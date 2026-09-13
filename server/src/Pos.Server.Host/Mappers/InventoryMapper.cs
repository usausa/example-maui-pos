namespace Pos.Server.Host.Mappers;

using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Shared.Inventory;

using Smart.Mapper;

public static partial class InventoryMapper
{
    [Mapper]
    public static partial InventoryLevelResponse ToLevelResponse(InventoryLevelEntity entity);

    [Mapper]
    public static partial ProductInventoryResponseLevel ToProductLevel(ProductInventoryLevel level);

    [Mapper]
    public static partial InventoryChangeResponse ToChangeResponse(InventoryChangeEntity entity);
}
