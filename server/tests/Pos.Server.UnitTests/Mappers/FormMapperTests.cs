namespace Pos.Server.Mappers;

using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// Entity ↔ Form の変換 (Guid ↔ Guid?、DateOnly ↔ DateTime)
public sealed class FormMapperTests
{
    [Fact]
    public void TerminalRoundTrip()
    {
        var entity = new TerminalEntity { Id = Guid.NewGuid(), StoreId = Guid.NewGuid(), TerminalNo = 3, Name = "レジ 3", IsActive = true, Version = 2 };

        var form = FormMapper.ToTerminalForm(entity);
        var mapped = FormMapper.ToTerminalEntity(form);

        Assert.Equal(entity.StoreId, form.StoreId);
        Assert.Equal(entity.StoreId, mapped.StoreId);
        Assert.Equal(3, mapped.TerminalNo);
        Assert.Equal("レジ 3", mapped.Name);
        Assert.Equal(2, mapped.Version);
        Assert.Equal(Guid.Empty, FormMapper.ToTerminalEntity(new TerminalForm()).StoreId);
    }

    [Fact]
    public void CustomerBirthDateConverts()
    {
        var entity = new CustomerEntity { Id = Guid.NewGuid(), Code = "M0001", Name = "山田", BirthDate = new DateOnly(1980, 4, 1) };

        var form = FormMapper.ToCustomerForm(entity);
        var mapped = FormMapper.ToCustomerEntity(form);

        Assert.Equal(new DateTime(1980, 4, 1), form.BirthDate);
        Assert.Equal(new DateOnly(1980, 4, 1), mapped.BirthDate);
        Assert.Null(FormMapper.ToCustomerEntity(new CustomerForm()).BirthDate);
    }

    [Fact]
    public void ProductSelectsConvert()
    {
        var categoryId = Guid.NewGuid();
        var taxRateId = Guid.NewGuid();
        var form = new ProductForm { Code = "X", Name = "商品", CategoryId = categoryId, TaxRateId = taxRateId, Price = 100m };

        var entity = FormMapper.ToProductEntity(form);
        var back = FormMapper.ToProductForm(entity);

        Assert.Equal(categoryId, entity.CategoryId);
        Assert.Equal(taxRateId, entity.TaxRateId);
        Assert.Equal(categoryId, back.CategoryId);
        Assert.Equal(100m, back.Price);
    }
}
