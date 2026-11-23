namespace Energoreestr.Check;

/// <summary>Наскільки серйозне зауваження.</summary>
public enum Level
{
    /// <summary>Файл прочитають неправильно, треба поправити.</summary>
    Fix,
    /// <summary>Може бути помилкою, вирішує людина.</summary>
    Verify,
    /// <summary>Не помилка, але корисно знати.</summary>
    Info
}

/// <summary>Вид зауваження: рівень і текст для людини.</summary>
public record CodeInfo(Level Level, string Text);

/// <summary>Одне зауваження до файлу.</summary>
/// <param name="Line">Номер рядка у файлі; 0, якщо зауваження до всього файлу.</param>
/// <param name="Column">Номер колонки від нуля або null, якщо колонку не визначено.</param>
/// <param name="Value">Значення з файлу. null, якщо його не можна показувати: рядок з ознаками
/// персональних даних або текстова клітинка.</param>
/// <param name="Note">Пояснення нашими словами, без значень з файлу.</param>
public record Finding(string Code, int Line, int? Column = null, string? Value = null, string? Note = null);

public enum ColumnKind { Unknown, Text, Number, Date, Edrpou, Eic, Iban }

public record ColumnProfile(int Index, string Name, ColumnKind Kind, int Filled);

public record FileProfile(string Encoding, char Delimiter, bool HasHeader, IReadOnlyList<ColumnProfile> Columns);

/// <summary>Результат перевірки файлу.</summary>
/// <param name="Rows">Скільки рядків з даними, без заголовка.</param>
/// <param name="PersonalDataRows">Скільки рядків мають ознаки персональних даних.</param>
/// <param name="File">Як прочитано файл; null у режимі конкретного реєстру.</param>
/// <param name="Problem">Чому файл не вдалося перевірити; тоді решта порожня.</param>
public record CheckReport(
    int Rows,
    IReadOnlyList<Finding> Findings,
    int PersonalDataRows,
    FileProfile? File = null,
    string? Problem = null)
{
    public static CheckReport Unreadable(string problem) => new(0, [], 0, Problem: problem);
}
