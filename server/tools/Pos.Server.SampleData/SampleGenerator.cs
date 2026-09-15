namespace Pos.Server.SampleData;

using Pos.Contract.Customers;
using Pos.Contract.Discounts;
using Pos.Contract.Inventory;
using Pos.Contract.PaymentMethods;
using Pos.Contract.Products;
using Pos.Contract.Shifts;
using Pos.Contract.Staff;
using Pos.Contract.Stores;
using Pos.Contract.Sync;
using Pos.Contract.TaxRates;
using Pos.Contract.Terminals;
using Pos.Contract.Transactions;
using Pos.Domain.Logic;

// 端末と同じ手順 (Pos.Domain で計算 → TransactionCreateRequest → POST) で、過去 N 日分のシフト・販売・返品・入出金・精算を作る
internal sealed class SampleGenerator
{
    private static readonly string[] PaidOutReasons = ["両替", "釣銭補充", "経費支払"];

    private static readonly string[] Recipients = ["山田 太郎", "佐藤 花子", "鈴木 一郎"];

    private readonly ApiClient client;

    private readonly SampleDataOptions options;

    private readonly TextWriter output;

    private readonly SampleRandom random;

    private SyncMastersResponse masters = default!;

    private Dictionary<Guid, TaxRateResponseItem> taxRates = [];

    private List<ProductResponseItem> products = [];

    private List<CustomerResponseItem> customers = [];

    private Dictionary<Guid, int> pointBalances = [];

    private List<DiscountResponseItem> lineDiscounts = [];

    private List<DiscountResponseItem> transactionDiscounts = [];

    private PaymentMethodResponseItem cash = default!;

    private PaymentMethodResponseItem? card;

    private PaymentMethodResponseItem? points;

    private TaxRounding taxRounding;

    private PointBasis pointBasis;

    private int created;

    public SampleGenerator(ApiClient client, SampleDataOptions options, TextWriter output)
    {
        this.client = client;
        this.options = options;
        this.output = output;
        random = new SampleRandom(options.Seed);
    }

    public async Task RunAsync()
    {
        await LoadMastersAsync().ConfigureAwait(false);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var stores = masters.Stores.Where(static x => x.IsActive && !x.IsDeleted).ToList();
        foreach (var store in stores)
        {
            var terminals = masters.Terminals.Where(x => (x.StoreId == store.Id) && x.IsActive && !x.IsDeleted).ToList();
            var staff = masters.Staff.Where(x => x.IsActive && !x.IsDeleted && ((x.StoreId == store.Id) || (x.StoreId is null))).ToList();
            var cashiers = staff.Where(static x => x.Role == StaffRole.Cashier).ToList();
            var managers = staff.Where(static x => x.Role is StaffRole.Manager or StaffRole.Admin).ToList();
            if ((terminals.Count == 0) || (staff.Count == 0))
            {
                await output.WriteLineAsync($"[{store.Name}] 端末またはスタッフがないため省略").ConfigureAwait(false);
                continue;
            }

            await ReceiveStockAsync(store, managers.Count > 0 ? managers[0] : staff[0], today.AddDays(-(options.Days - 1))).ConfigureAwait(false);

            foreach (var terminal in terminals)
            {
                var current = await client.GetOrDefaultAsync<ShiftResponseItem>($"shifts/current?terminalId={terminal.Id}").ConfigureAwait(false);
                if (current is { Status: ShiftStatus.Open })
                {
                    await output.WriteLineAsync($"[{store.Name} {terminal.Name}] 開設中のシフトがあるため省略").ConfigureAwait(false);
                    continue;
                }

                var latest = await client.GetAsync<TerminalResponseItem>($"terminals/{terminal.Id}").ConfigureAwait(false);
                var receiptSeq = latest.LastReceiptSeq;
                for (var offset = options.Days - 1; offset >= 0; offset--)
                {
                    var date = today.AddDays(-offset);
                    var cashier = Pick(cashiers.Count > 0 ? cashiers : staff);
                    var manager = managers.Count > 0 ? Pick(managers) : cashier;
                    await GenerateDayAsync(store, terminal, cashier, manager, date, () => $"{store.Code}-{terminal.TerminalNo:00}-{++receiptSeq:000000}").ConfigureAwait(false);
                }
            }
        }

        await output.WriteLineAsync($"完了: 取引 {created} 件").ConfigureAwait(false);
    }

    private async Task LoadMastersAsync()
    {
        masters = await client.GetAsync<SyncMastersResponse>("sync/masters").ConfigureAwait(false);
        taxRates = masters.TaxRates.Where(static x => !x.IsDeleted).ToDictionary(static x => x.Id);
        products = masters.Products.Where(x => x.IsActive && !x.IsDeleted && taxRates.ContainsKey(x.TaxRateId)).ToList();
        if (masters.ProductsTruncated)
        {
            var page = await client.GetAsync<ProductResponse>("products?size=1000").ConfigureAwait(false);
            products = page.Items.Where(x => x.IsActive && !x.IsDeleted && taxRates.ContainsKey(x.TaxRateId)).ToList();
        }

        var discounts = masters.Discounts.Where(static x => x.IsActive && !x.IsDeleted).ToList();
        lineDiscounts = discounts.Where(static x => x.Scope == DiscountScope.Line).ToList();
        transactionDiscounts = discounts.Where(static x => x.Scope == DiscountScope.Transaction).ToList();

        var methods = masters.PaymentMethods.Where(static x => x.IsActive && !x.IsDeleted).ToList();
        cash = methods.FirstOrDefault(static x => x.Kind == PaymentKind.Cash) ?? throw new ApiException("現金の支払方法がありません");
        card = methods.FirstOrDefault(static x => x.Kind == PaymentKind.Card);
        points = methods.FirstOrDefault(static x => x.Kind == PaymentKind.Points);

        taxRounding = masters.Settings?.TaxRounding ?? TaxRounding.Floor;
        pointBasis = masters.Settings?.PointBasis ?? PointBasis.TaxIncluded;

        var customerList = await client.GetAsync<CustomerResponse>("customers?size=100").ConfigureAwait(false);
        customers = customerList.Items.Where(static x => !x.IsDeleted).ToList();
        pointBalances = customers.ToDictionary(static x => x.Id, static x => x.PointBalance);

        if (products.Count == 0)
        {
            throw new ApiException("商品がありません");
        }

        await output.WriteLineAsync($"マスタ: 店舗 {masters.Stores.Count} / 端末 {masters.Terminals.Count} / 商品 {products.Count} / 会員 {customers.Count}").ConfigureAwait(false);
    }

    // 初日の開店前に在庫を積む (販売で在庫がマイナスになりすぎないように、在庫調整で入荷扱い)
    private async Task ReceiveStockAsync(StoreResponseItem store, StaffResponseItem staff, DateOnly date)
    {
        var changes = products
            .Where(static x => x.TrackInventory && (x.Kind == ProductKind.Goods))
            .Select(x => new InventoryChangeRequestChange
            {
                Id = Guid.NewGuid(),
                StoreId = store.Id,
                ProductId = x.Id,
                Type = InventoryChangeType.Adjustment,
                Quantity = 10 + random.Next(21),
                Reason = "サンプル入荷",
                StaffId = staff.Id,
                OccurredAt = ToUtc(date, 8, 30)
            })
            .ToList();
        if (changes.Count == 0)
        {
            return;
        }

        await client.PostAsync<InventoryChangeResultResponse>("inventory/changes", new InventoryChangeRequest { Changes = changes }).ConfigureAwait(false);
        await output.WriteLineAsync($"[{store.Name}] 入荷 {changes.Count} 商品").ConfigureAwait(false);
    }

    // 1 日分: 開設 → 販売 (返品・取消を混ぜる) → 出金 → 精算
    private async Task GenerateDayAsync(StoreResponseItem store, TerminalResponseItem terminal, StaffResponseItem cashier, StaffResponseItem manager, DateOnly date, Func<string> nextReceiptNo)
    {
        var openedAt = ToUtc(date, 9, 0);
        var shift = new ShiftOpenRequest
        {
            Id = Guid.NewGuid(),
            StoreId = store.Id,
            TerminalId = terminal.Id,
            BusinessDate = date,
            OpenedAt = openedAt,
            OpenedByStaffId = cashier.Id,
            OpeningCash = 30000m
        };
        await client.PostAsync<ShiftResponseItem>("shifts", shift).ConfigureAwait(false);

        var count = Math.Max(1, options.PerDay + random.Next(-2, 3));
        var sales = new List<TransactionResponseItem>();
        var cashTotal = 0m;
        for (var i = 0; i < count; i++)
        {
            var at = openedAt.AddMinutes(30 + (i * (600.0 / count)) + random.Next(0, 20));
            var (request, cashDelta) = BuildSale(store, terminal, cashier, manager, shift.Id, date, at, nextReceiptNo());
            var response = await PostTransactionAsync(request).ConfigureAwait(false);
            if (response is null)
            {
                continue;
            }

            sales.Add(response);
            cashTotal += cashDelta;
            Apply(response);

            // 返品 (ときどき、直前までの販売から 1 明細)
            if ((sales.Count > 1) && (random.Next(100) < 25))
            {
                var original = sales[random.Next(sales.Count - 1)];
                var (returnRequest, refund) = BuildReturn(original, store, terminal, cashier, shift.Id, date, at.AddMinutes(10), nextReceiptNo());
                if (returnRequest is not null)
                {
                    var returned = await PostTransactionAsync(returnRequest).ConfigureAwait(false);
                    if (returned is not null)
                    {
                        cashTotal -= refund;
                        Apply(returned);
                        sales.Remove(original);
                    }
                }
            }
        }

        // 取消 (ときどき)
        if ((sales.Count > 2) && (random.Next(100) < 30))
        {
            var target = sales[^1];
            var voided = await PostVoidAsync(target, cashier, openedAt.AddMinutes(700)).ConfigureAwait(false);
            if (voided)
            {
                cashTotal -= target.Payments.Where(static x => x.Kind == PaymentKind.Cash).Sum(static x => x.Amount);
                Apply(target, revert: true);
            }
        }

        // 出金
        var paidOut = 0m;
        if (random.Next(100) < 60)
        {
            paidOut = random.Next(1, 4) * 5000m;
            await client.PostAsync<ShiftCashEventResponseItem>($"shifts/{shift.Id}/cash-events", new ShiftCashEventRequest
            {
                Id = Guid.NewGuid(),
                Type = CashEventType.PaidOut,
                Amount = paidOut,
                Reason = Pick(PaidOutReasons),
                StaffId = cashier.Id,
                OccurredAt = openedAt.AddMinutes(400)
            }).ConfigureAwait(false);
        }

        // 精算 (過不足はときどき)
        var expected = shift.OpeningCash + cashTotal - paidOut;
        var difference = random.Next(100) < 20 ? random.Next(-3, 3) * 50m : 0m;
        await client.PostAsync<ShiftResponseItem>($"shifts/{shift.Id}/close", new ShiftCloseRequest
        {
            ClosedAt = ToUtc(date, 20, 0),
            ClosedByStaffId = cashier.Id,
            ActualCash = expected + difference
        }).ConfigureAwait(false);

        await output.WriteLineAsync($"{date:yyyy-MM-dd} {store.Name} {terminal.Name}: 販売 {count} 件, 現金 {expected:#,##0}").ConfigureAwait(false);
    }

    //--------------------------------------------------------------------------------
    // Sale
    //--------------------------------------------------------------------------------

    private (TransactionCreateRequest Request, decimal CashDelta) BuildSale(StoreResponseItem store, TerminalResponseItem terminal, StaffResponseItem cashier, StaffResponseItem manager, Guid shiftId, DateOnly date, DateTime at, string receiptNo)
    {
        var customer = (customers.Count > 0) && (random.Next(100) < 35) ? Pick(customers) : null;
        var lineCount = random.Next(1, 4);
        var lines = new List<SalesInputLine>();
        var lineProducts = new List<ProductResponseItem>();
        var discounts = new List<TransactionCreateRequestDiscount>();
        var inputDiscounts = new List<SalesInputDiscount>();
        var serials = new Dictionary<Guid, List<string>>();

        for (var i = 0; i < lineCount; i++)
        {
            var product = Pick(products);
            if (lineProducts.Any(x => x.Id == product.Id))
            {
                continue;
            }

            var quantity = product.Kind == ProductKind.Service ? 1m : random.Next(1, 4);
            var lineId = Guid.NewGuid();
            lines.Add(new SalesInputLine
            {
                Id = lineId,
                LineNo = lines.Count + 1,
                ProductId = product.Id,
                ListPrice = product.Price,
                UnitPrice = product.Price,
                Quantity = quantity,
                TaxRateId = product.TaxRateId,
                TaxRate = taxRates[product.TaxRateId].Rate,
                TaxIncluded = product.TaxIncluded,
                PointRate = customer is null ? 0m : product.PointRate
            });
            lineProducts.Add(product);

            if (product.RequiresSerial)
            {
                serials[lineId] = Enumerable.Range(0, (int)quantity).Select(_ => $"SN-{random.Next(100000, 999999)}").ToList();
            }

            // 明細値引 (定義済み。承認が必要なものは店長を承認者に)
            if ((lineDiscounts.Count > 0) && (random.Next(100) < 20))
            {
                var definition = Pick(lineDiscounts);
                var id = Guid.NewGuid();
                inputDiscounts.Add(new SalesInputDiscount { Id = id, LineId = lineId, Type = definition.Type, Value = definition.Value });
                discounts.Add(new TransactionCreateRequestDiscount
                {
                    Id = id,
                    LineId = lineId,
                    DiscountId = definition.Id,
                    Name = definition.Name,
                    Type = definition.Type,
                    Value = definition.Value,
                    ApprovedByStaffId = definition.RequiresApproval ? manager.Id : null
                });
            }
        }

        // 取引値引 (定義済み or 端数値引)
        if (random.Next(100) < 15)
        {
            var id = Guid.NewGuid();
            if ((transactionDiscounts.Count > 0) && (random.Next(2) == 0))
            {
                var definition = Pick(transactionDiscounts);
                inputDiscounts.Add(new SalesInputDiscount { Id = id, Type = definition.Type, Value = definition.Value });
                discounts.Add(new TransactionCreateRequestDiscount { Id = id, DiscountId = definition.Id, Name = definition.Name, Type = definition.Type, Value = definition.Value, ApprovedByStaffId = definition.RequiresApproval ? manager.Id : null });
            }
            else
            {
                inputDiscounts.Add(new SalesInputDiscount { Id = id, Type = DiscountType.Amount, Value = 100m });
                discounts.Add(new TransactionCreateRequestDiscount { Id = id, Name = "端数値引", Type = DiscountType.Amount, Value = 100m, Reason = "端数" });
            }
        }

        // 支払: 金額を決めるため一度計算してから支払を組む
        var provisional = SalesLogic.Calculate(new SalesInput { TaxRounding = taxRounding, PointBasis = pointBasis, Lines = lines, Discounts = inputDiscounts });
        var payments = new List<SalesInputPayment>();
        var requestPayments = new List<TransactionCreateRequestPayment>();
        var remaining = provisional.Total;

        var balance = customer is null ? 0 : pointBalances.GetValueOrDefault(customer.Id);
        if ((points is not null) && (customer is not null) && (balance >= 500) && (random.Next(100) < 50))
        {
            var use = Math.Min(Math.Min(balance, (int)remaining), random.Next(1, 6) * 500);
            if (use > 0)
            {
                AddPayment(payments, requestPayments, points, use, use, null);
                remaining -= use;
            }
        }

        var cashDelta = 0m;
        if (remaining > 0)
        {
            if ((card is not null) && (random.Next(100) < 35))
            {
                AddPayment(payments, requestPayments, card, remaining, remaining, $"{random.Next(1000, 9999)}-{random.Next(1000, 9999)}");
            }
            else
            {
                var tendered = Math.Ceiling(remaining / 1000m) * 1000m;
                AddPayment(payments, requestPayments, cash, remaining, tendered, null);
                cashDelta = remaining;
            }
        }

        var input = new SalesInput { TaxRounding = taxRounding, PointBasis = pointBasis, Lines = lines, Discounts = inputDiscounts, Payments = payments };
        var result = SalesLogic.Calculate(input);

        var request = new TransactionCreateRequest
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Sale,
            Status = TransactionStatus.Completed,
            StoreId = store.Id,
            TerminalId = terminal.Id,
            StaffId = cashier.Id,
            ShiftId = shiftId,
            CustomerId = customer?.Id,
            ReceiptNo = receiptNo,
            BusinessDate = date,
            TransactedAt = at,
            Lines = lines.Select((x, i) => ToRequestLine(x, lineProducts[i], result.Lines[i], serials.GetValueOrDefault(x.Id), null)).ToList(),
            Discounts = discounts.Select(x =>
            {
                x.Amount = result.Discounts.First(d => d.Id == x.Id).Amount;
                return x;
            }).ToList(),
            TaxSummaries = ToTaxSummaries(result),
            Subtotal = result.Subtotal,
            DiscountTotal = result.DiscountTotal,
            NetSubtotal = result.NetSubtotal,
            TaxTotal = result.TaxTotal,
            Total = result.Total,
            Payments = requestPayments,
            TenderedTotal = result.TenderedTotal,
            ChangeAmount = result.ChangeAmount,
            PointsEarned = result.PointsEarned,
            PointsRedeemed = result.PointsRedeemed,
            Delivery = lineProducts.Any(static x => x.Kind == ProductKind.Service) && (random.Next(100) < 50)
                ? new TransactionCreateRequestDelivery { RecipientName = Pick(Recipients), Address = "東京都千代田区千代田 1-1", RequestedDate = date.AddDays(3), TimeSlot = "14-16 時" }
                : null
        };
        return (request, cashDelta);
    }

    //--------------------------------------------------------------------------------
    // Return
    //--------------------------------------------------------------------------------

    private (TransactionCreateRequest? Request, decimal CashRefund) BuildReturn(TransactionResponseItem original, StoreResponseItem store, TerminalResponseItem terminal, StaffResponseItem cashier, Guid shiftId, DateOnly date, DateTime at, string receiptNo)
    {
        if (original.Status != TransactionStatus.Completed)
        {
            return (null, 0m);
        }

        var line = original.Lines.FirstOrDefault(static x => x.Quantity > x.ReturnedQuantity);
        if (line is null)
        {
            return (null, 0m);
        }

        var input = new ReturnInput
        {
            TaxRounding = taxRounding,
            OriginalLines = original.Lines.Select(static x => new ReturnOriginalLine
            {
                Id = x.Id,
                UnitPrice = x.UnitPrice,
                Quantity = x.Quantity,
                ReturnedQuantity = x.ReturnedQuantity,
                DiscountAmount = x.DiscountAmount,
                AllocatedDiscountAmount = x.AllocatedDiscountAmount,
                PointsEarned = x.PointsEarned,
                PointsRedeemed = x.PointsRedeemed,
                TaxRateId = x.TaxRateId,
                TaxRate = x.TaxRate,
                TaxIncluded = x.TaxIncluded
            }).ToList(),
            Lines = [new ReturnInputLine { Id = Guid.NewGuid(), LineNo = 1, OriginalLineId = line.Id, Quantity = 1m }]
        };
        var provisional = ReturnLogic.Calculate(input);
        if (provisional.Total <= 0)
        {
            return (null, 0m);
        }

        // ポイント返還 + 残りは現金
        var payments = new List<SalesInputPayment>();
        var requestPayments = new List<TransactionCreateRequestPayment>();
        var pointsRefund = -provisional.PointsRedeemed;
        if (pointsRefund > 0)
        {
            if (points is null)
            {
                return (null, 0m);
            }

            AddPayment(payments, requestPayments, points, pointsRefund, pointsRefund, null);
        }

        var cashRefund = provisional.Total - pointsRefund;
        if (cashRefund > 0)
        {
            AddPayment(payments, requestPayments, cash, cashRefund, cashRefund, null);
        }

        input = input with { Payments = payments };
        var result = ReturnLogic.Calculate(input);
        var product = products.FirstOrDefault(x => x.Id == line.ProductId);

        var request = new TransactionCreateRequest
        {
            Id = Guid.NewGuid(),
            Type = TransactionType.Return,
            Status = TransactionStatus.Completed,
            StoreId = store.Id,
            TerminalId = terminal.Id,
            StaffId = cashier.Id,
            ShiftId = shiftId,
            CustomerId = original.CustomerId,
            ReceiptNo = receiptNo,
            BusinessDate = date,
            TransactedAt = at,
            OriginalTransactionId = original.Id,
            Lines =
            [
                new TransactionCreateRequestLine
                {
                    Id = input.Lines[0].Id,
                    LineNo = 1,
                    ProductId = line.ProductId,
                    ProductCode = line.ProductCode,
                    ProductName = line.ProductName,
                    CategoryId = line.CategoryId,
                    Kind = product?.Kind ?? line.Kind,
                    ListPrice = line.ListPrice,
                    UnitPrice = line.UnitPrice,
                    Quantity = 1m,
                    TaxRateId = line.TaxRateId,
                    TaxRate = line.TaxRate,
                    TaxIncluded = line.TaxIncluded,
                    PointRate = line.PointRate,
                    Amount = result.Lines[0].Amount,
                    DiscountAmount = result.Lines[0].DiscountAmount,
                    AllocatedDiscountAmount = result.Lines[0].AllocatedDiscountAmount,
                    NetAmount = result.Lines[0].NetAmount,
                    PointsRedeemed = result.Lines[0].PointsRedeemed,
                    PointsEarned = result.Lines[0].PointsEarned,
                    OriginalLineId = line.Id
                }
            ],
            TaxSummaries = ToTaxSummaries(result),
            Subtotal = result.Subtotal,
            DiscountTotal = result.DiscountTotal,
            NetSubtotal = result.NetSubtotal,
            TaxTotal = result.TaxTotal,
            Total = result.Total,
            Payments = requestPayments,
            TenderedTotal = result.TenderedTotal,
            ChangeAmount = result.ChangeAmount,
            PointsEarned = result.PointsEarned,
            PointsRedeemed = result.PointsRedeemed,
            Note = "お客様都合"
        };
        return (request, cashRefund);
    }

    //--------------------------------------------------------------------------------
    // Post
    //--------------------------------------------------------------------------------

    private async Task<TransactionResponseItem?> PostTransactionAsync(TransactionCreateRequest request)
    {
        try
        {
            var response = await client.PostAsync<TransactionResponseItem>("transactions", request).ConfigureAwait(false);
            created++;
            return response;
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
        {
            await output.WriteLineAsync($"  取引を省略: {ex.Message}").ConfigureAwait(false);
            return null;
        }
    }

    private async Task<bool> PostVoidAsync(TransactionResponseItem target, StaffResponseItem cashier, DateTime at)
    {
        try
        {
            await client.PostAsync<TransactionResponseItem>($"transactions/{target.Id}/void", new TransactionVoidRequest { StaffId = cashier.Id, Reason = "登録誤り", VoidedAt = at }).ConfigureAwait(false);
            return true;
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
        {
            await output.WriteLineAsync($"  取消を省略: {ex.Message}").ConfigureAwait(false);
            return false;
        }
    }

    // 会員のポイント残高を追いかける (次の販売で使える額を決めるため)
    private void Apply(TransactionResponseItem response, bool revert = false)
    {
        if (response.CustomerId is null)
        {
            return;
        }

        var delta = response.PointsEarned - response.PointsRedeemed;
        pointBalances[response.CustomerId.Value] = pointBalances.GetValueOrDefault(response.CustomerId.Value) + (revert ? -delta : delta);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static void AddPayment(List<SalesInputPayment> payments, List<TransactionCreateRequestPayment> requestPayments, PaymentMethodResponseItem method, decimal amount, decimal tendered, string? reference)
    {
        var id = Guid.NewGuid();
        payments.Add(new SalesInputPayment { Id = id, Kind = method.Kind, Amount = amount, TenderedAmount = tendered, AllowsChange = method.AllowsChange });
        requestPayments.Add(new TransactionCreateRequestPayment { Id = id, SeqNo = requestPayments.Count + 1, PaymentMethodId = method.Id, Kind = method.Kind, Amount = amount, TenderedAmount = tendered, Reference = reference });
    }

    private static TransactionCreateRequestLine ToRequestLine(SalesInputLine line, ProductResponseItem product, SalesResultLine calculated, List<string>? serials, Guid? originalLineId) => new()
    {
        Id = line.Id,
        LineNo = line.LineNo,
        ProductId = product.Id,
        ProductCode = product.Code,
        ProductName = product.Name,
        CategoryId = product.CategoryId,
        Kind = product.Kind,
        ListPrice = line.ListPrice,
        UnitPrice = line.UnitPrice,
        Quantity = line.Quantity,
        TaxRateId = line.TaxRateId,
        TaxRate = line.TaxRate,
        TaxIncluded = line.TaxIncluded,
        PointRate = line.PointRate,
        Amount = calculated.Amount,
        DiscountAmount = calculated.DiscountAmount,
        AllocatedDiscountAmount = calculated.AllocatedDiscountAmount,
        NetAmount = calculated.NetAmount,
        PointsRedeemed = calculated.PointsRedeemed,
        PointsEarned = calculated.PointsEarned,
        SerialNumbers = serials ?? [],
        OriginalLineId = originalLineId
    };

    private static List<TransactionCreateRequestTaxSummary> ToTaxSummaries(SalesResult result) =>
        result.TaxSummaries.Select(static x => new TransactionCreateRequestTaxSummary { TaxRateId = x.TaxRateId, Rate = x.Rate, TaxIncluded = x.TaxIncluded, TaxableAmount = x.TaxableAmount, TaxAmount = x.TaxAmount }).ToList();

    // ローカル時刻 (店舗の営業時間) を UTC に
    private static DateTime ToUtc(DateOnly date, int hour, int minute) =>
        new DateTime(date, new TimeOnly(hour, minute), DateTimeKind.Local).ToUniversalTime();

    private T Pick<T>(IReadOnlyList<T> items) => items[random.Next(items.Count)];
}
