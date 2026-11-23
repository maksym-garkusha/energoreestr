using System.Text;

namespace Energoreestr.Csv;

/// <summary>Один запис CSV: поля та номер рядка у файлі, з якого він почався.</summary>
public record CsvRecord(int Line, string[] Fields)
{
    /// <summary>
    /// Номер рядка таблиці, як його покаже Excel: рахуються записи разом з порожніми рядками,
    /// а перенесення всередині клітинки в лапках рахунку не збиває.
    /// </summary>
    public int Row { get; init; }
}

/// <summary>
/// Читає CSV посимвольно. Лапки працюють як у RFC 4180: поле в лапках може містити
/// роздільник і перенесення рядка, а подвійні лапки всередині означають одну.
/// Нічого не обрізає і не "виправляє": що у файлі, те й повертає.
/// </summary>
public static class CsvReader
{
    public static List<CsvRecord> Read(string text, char delimiter) => Read(text, delimiter, out _);

    /// <param name="unclosedQuoteLine">Рядок, де відкрито лапки, які так і не закрилися до кінця файлу.</param>
    public static List<CsvRecord> Read(string text, char delimiter, out int? unclosedQuoteLine)
    {
        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();

        int line = 1;
        int recordLine = 1;
        int row = 1;
        bool inQuotes = false;
        bool fieldStarted = false;   // чи вже щось прочитали в поточному полі

        // BOM на початку файлу є в усіх наборах НКРЕКП
        int i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;

        for (; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (c == '\n') line++;
                    field.Append(c);
                }
                continue;
            }

            if (c == '"' && !fieldStarted)
            {
                inQuotes = true;
                fieldStarted = true;
            }
            else if (c == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
                fieldStarted = false;
            }
            else if (c == '\n' || c == '\r')
            {
                // \r\n рахуємо як один кінець рядка
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;

                fields.Add(field.ToString());
                field.Clear();
                fieldStarted = false;

                // порожні рядки пропускаємо
                if (fields.Count > 1 || fields[0].Length > 0)
                    records.Add(new CsvRecord(recordLine, fields.ToArray()) { Row = row });
                fields.Clear();

                line++;
                row++;
                recordLine = line;
            }
            else
            {
                field.Append(c);
                fieldStarted = true;
            }
        }

        unclosedQuoteLine = inQuotes ? recordLine : null;

        // останній рядок без перенесення в кінці
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(new CsvRecord(recordLine, fields.ToArray()) { Row = row });
        }

        return records;
    }
}
