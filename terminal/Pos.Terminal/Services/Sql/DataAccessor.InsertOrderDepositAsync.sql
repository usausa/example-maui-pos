INSERT INTO
    OrderDeposits
    (Id, OrderId, ShiftId, Type, PaymentMethodId, Kind, Amount, OccurredAt)
VALUES
    (/*@ id */'', /*@ orderId */'', /*@ shiftId */'', /*@ type */'', /*@ paymentMethodId */'', /*@ kind */'', /*@ amount */0, /*@ occurredAt */0)
ON CONFLICT (Id) DO NOTHING
