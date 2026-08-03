using System.Globalization;

namespace PokemonBattleJournal.Controls;

internal sealed class StringNotEmptyConverter : IValueConverter
{
    internal static readonly StringNotEmptyConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrEmpty(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
