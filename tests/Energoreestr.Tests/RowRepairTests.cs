using Energoreestr.Csv;

namespace Energoreestr.Tests;

public class RowRepairTests
{
    // маленький "реєстр" на чотири колонки: номер, назва, місто, сайт
    private static readonly RowRepair Repair = new(
    [
        s => s.All(char.IsDigit),
        null,
        s => s.All(char.IsLetter),
        s => s.Contains('.')
    ]);

    [Fact]
    public void Piece_starting_with_space_continues_previous_column()
    {
        var result = Repair.Repair(["7", "Комісія", " що регулює", "Київ", "nerc.gov.ua"]);

        Assert.Equal(RepairKind.Glued, result.Kind);
        Assert.Equal(["7", "Комісія, що регулює", "Київ", "nerc.gov.ua"], result.Fields);
    }

    [Fact]
    public void Comma_without_space_is_resolved_by_how_columns_look()
    {
        // "Д,С." розрізало назву навпіл, пробілу після коми немає
        var result = Repair.Repair(["7", "ГАЗ Д", "С.", "Кишинів", "n-gaz.com"]);

        Assert.Equal(RepairKind.Rearranged, result.Kind);
        Assert.Equal(["7", "ГАЗ Д,С.", "Кишинів", "n-gaz.com"], result.Fields);
    }

    [Fact]
    public void Column_glued_to_previous_one_is_cut_back()
    {
        // сайт записали через кому з пробілом одразу після міста
        var result = Repair.Repair(["7", "Енерго", "Львів", " energo.lviv.ua"]);

        Assert.Equal(RepairKind.Rearranged, result.Kind);
        Assert.Equal(["7", "Енерго", "Львів", "energo.lviv.ua"], result.Fields);
    }

    [Fact]
    public void Reports_when_several_splits_fit_equally()
    {
        // про дві середні колонки нічого не відомо, тому і "А,Б | В", і "А | Б,В" підходять
        var loose = new RowRepair([s => s.All(char.IsDigit), null, null, s => s.Contains('.')]);

        var result = loose.Repair(["7", "А", "Б", "В", "site.ua"]);

        Assert.Equal(RepairKind.Ambiguous, result.Kind);
        Assert.Equal(4, result.Fields.Length);
    }

    [Fact]
    public void Fails_when_there_are_fewer_pieces_than_columns()
    {
        var result = Repair.Repair(["7", "Енерго", "Львів"]);

        Assert.Equal(RepairKind.Failed, result.Kind);
    }
}
