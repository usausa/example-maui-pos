namespace Pos.Terminal.Markup;

using Fonts;

public static class AppIcons
{
    private const double MethodSize = 18d;

    private const double CartSize = 30d;

    private const double CashEventSize = 20d;

    public static readonly FontImageSource PosSearch = Create(MaterialIcons.Search, MethodSize, Colors.White);

    public static readonly FontImageSource PosDialpad = Create(MaterialIcons.Dialpad, MethodSize, Colors.White);

    public static readonly FontImageSource PosCart = Create(MaterialIcons.Shopping_cart, CartSize, Colors.White);

    public static readonly FontImageSource PaidIn = Create(MaterialIcons.Arrow_downward, CashEventSize, Color.FromArgb("#DE000000"));

    public static readonly FontImageSource PaidInSelected = Create(MaterialIcons.Arrow_downward, CashEventSize, Colors.White);

    public static readonly FontImageSource PaidOut = Create(MaterialIcons.Arrow_upward, CashEventSize, Color.FromArgb("#DE000000"));

    public static readonly FontImageSource PaidOutSelected = Create(MaterialIcons.Arrow_upward, CashEventSize, Colors.White);

    public static readonly FontImageSource NoSale = Create(MaterialIcons.Point_of_sale, CashEventSize, Color.FromArgb("#DE000000"));

    public static readonly FontImageSource NoSaleSelected = Create(MaterialIcons.Point_of_sale, CashEventSize, Colors.White);

    private static FontImageSource Create(string glyph, double size, Color color) =>
        new()
        {
            FontFamily = MaterialIcons.FontFamily,
            Glyph = glyph,
            Size = size,
            Color = color
        };
}
