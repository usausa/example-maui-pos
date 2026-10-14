namespace Pos.Server.Models.Forms;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// Entity ↔ Form の変換 (Guid ↔ Guid?、DateOnly ↔ DateTime)
public sealed class FormMapperTests
{
    // 端末のフォームはエンティティと往復しても値が変わらず、店舗の未選択は Guid.Empty にする
    [Fact]
    public void TerminalRoundTrip()
    {
        // Arrange
        var entity = new TerminalEntity { Id = Guid.NewGuid(), StoreId = Guid.NewGuid(), TerminalNo = 3, Name = "レジ 3", IsActive = true, Version = 2 };

        // Act
        var form = TerminalForm.ToForm(entity);
        var mapped = TerminalForm.ToEntity(form);

        // Assert
        Assert.Equal(entity.StoreId, form.StoreId);
        Assert.Equal(entity.StoreId, mapped.StoreId);
        Assert.Equal(3, mapped.TerminalNo);
        Assert.Equal("レジ 3", mapped.Name);
        Assert.Equal(2, mapped.Version);
        Assert.Equal(Guid.Empty, TerminalForm.ToEntity(new TerminalForm()).StoreId);
    }

    // 会員の生年月日は DateOnly と DateTime を変換する (空は null)
    [Fact]
    public void CustomerBirthDateConverts()
    {
        // Arrange
        var entity = new CustomerEntity { Id = Guid.NewGuid(), Code = "M0001", Name = "山田", BirthDate = new DateOnly(1980, 4, 1) };

        // Act
        var form = CustomerForm.ToForm(entity);
        var mapped = CustomerForm.ToEntity(form);

        // Assert
        Assert.Equal(new DateTime(1980, 4, 1), form.BirthDate);
        Assert.Equal(new DateOnly(1980, 4, 1), mapped.BirthDate);
        Assert.Null(CustomerForm.ToEntity(new CustomerForm()).BirthDate);
    }

    // 商品の部門と税率の選択はエンティティと往復しても変わらない
    [Fact]
    public void ProductSelectsConvert()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var taxRateId = Guid.NewGuid();
        var form = new ProductForm { Code = "X", Name = "商品", CategoryId = categoryId, TaxRateId = taxRateId, Price = 100m };

        // Act
        var entity = ProductForm.ToEntity(form);
        var back = ProductForm.ToForm(entity);

        // Assert
        Assert.Equal(categoryId, entity.CategoryId);
        Assert.Equal(taxRateId, entity.TaxRateId);
        Assert.Equal(categoryId, back.CategoryId);
        Assert.Equal(100m, back.Price);
    }
}
