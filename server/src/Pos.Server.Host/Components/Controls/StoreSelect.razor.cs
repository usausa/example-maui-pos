namespace Pos.Server.Host.Components.Controls;

using Microsoft.AspNetCore.Components;

using Pos.Server.Models.Entity;
using Pos.Server.Services;

// 店舗セレクタ (null = 全店舗)
public sealed partial class StoreSelect
{
    private List<StoreEntity> stores = [];

    [Inject]
    public required StoreService StoreService { get; set; }

    [Parameter]
    public Guid? Value { get; set; }

    [Parameter]
    public EventCallback<Guid?> ValueChanged { get; set; }

    [Parameter]
    public bool AllowAll { get; set; } = true;

    [Parameter]
    public string Label { get; set; } = "店舗";

    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    protected override async Task OnInitializedAsync()
    {
        stores = await StoreService.QueryAllAsync(false, CancellationToken.None);
    }

    private Task OnValueChanged(Guid? value)
    {
        Value = value;
        return ValueChanged.InvokeAsync(value);
    }
}
