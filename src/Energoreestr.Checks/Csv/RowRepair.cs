namespace Energoreestr.Csv;

public enum RepairKind
{
    /// <summary>Вистачило правила про пробіл після коми.</summary>
    Glued,
    /// <summary>Правило дало не ту кількість колонок, межі підібрано за виглядом значень.</summary>
    Rearranged,
    /// <summary>Те саме, але однаково добре підходило кілька варіантів.</summary>
    Ambiguous,
    /// <summary>Розкласти рядок на колонки не вдалося.</summary>
    Failed
}

public record RepairResult(string[] Fields, RepairKind Kind);

/// <summary>
/// Збирає рядок назад у потрібну кількість колонок, коли у файлі коми всередині
/// значень не взяті в лапки.
///
/// Спостереження, на якому все тримається: всередині значення після коми люди
/// ставлять пробіл ("Національна комісія, що здійснює..."), а між колонками
/// пробілу немає. Тому шматок, що починається з пробілу, майже завжди є
/// продовженням попереднього.
///
/// "Майже" це близько 1% рядків. Для них перебираємо інші варіанти меж і беремо
/// той, де найбільше колонок схожі на себе (дата на місці дати, код на місці коду).
/// </summary>
public class RowRepair
{
    // більше ніж стільки меж за раз не рухаємо: перебір росте дуже швидко,
    // а в реальних файлах більше трьох не траплялося
    private const int MaxMoves = 4;

    private readonly int _columns;
    private readonly Func<string, bool>?[] _checks;

    /// <param name="checks">Перевірка для кожної колонки або null, якщо перевіряти нічого.</param>
    public RowRepair(Func<string, bool>?[] checks)
    {
        _columns = checks.Length;
        _checks = checks;
    }

    public RepairResult Repair(IReadOnlyList<string> pieces)
    {
        int needCuts = _columns - 1;
        if (pieces.Count < _columns)
            return new RepairResult([], RepairKind.Failed);

        // межа перед i-м шматком є, якщо він не починається з пробілу чи переносу
        var cuts = new List<int>();
        var notCuts = new List<int>();
        for (int i = 1; i < pieces.Count; i++)
        {
            if (pieces[i].Length > 0 && char.IsWhiteSpace(pieces[i][0])) notCuts.Add(i);
            else cuts.Add(i);
        }

        if (cuts.Count == needCuts)
            return new RepairResult(Build(pieces, cuts), RepairKind.Glued);

        // меж забагато: якісь треба прибрати; замало: додати з тих, що склеїлись
        bool tooMany = cuts.Count > needCuts;
        int moves = Math.Abs(cuts.Count - needCuts);
        var pool = tooMany ? cuts : notCuts;
        if (moves > MaxMoves || moves > pool.Count)
            return new RepairResult([], RepairKind.Failed);

        string[]? best = null;
        int bestScore = -1;
        int sameScore = 0;

        foreach (var chosen in Combinations(pool, moves))
        {
            var candidateCuts = tooMany
                ? cuts.Except(chosen).ToList()
                : cuts.Concat(chosen).Order().ToList();

            var fields = Build(pieces, candidateCuts);
            int score = Score(fields);

            if (score > bestScore)
            {
                best = fields;
                bestScore = score;
                sameScore = 1;
            }
            else if (score == bestScore)
            {
                // При рівному рахунку беремо варіант, де межі стоять правіше.
                // Комбінації йдуть за зростанням позицій: коли межі додаємо, такий
                // варіант трапляється останнім, а коли прибираємо, то першим.
                sameScore++;
                if (!tooMany) best = fields;
            }
        }

        if (best is null)
            return new RepairResult([], RepairKind.Failed);

        return new RepairResult(best, sameScore > 1 ? RepairKind.Ambiguous : RepairKind.Rearranged);
    }

    private int Score(string[] fields)
    {
        int score = 0;
        for (int i = 0; i < _columns; i++)
        {
            var check = _checks[i];
            if (check is null || check(fields[i])) score++;
        }
        return score;
    }

    private static string[] Build(IReadOnlyList<string> pieces, List<int> cuts)
    {
        var fields = new string[cuts.Count + 1];
        int field = 0;
        int next = 0;   // індекс наступної межі в cuts
        var current = pieces[0];

        for (int i = 1; i < pieces.Count; i++)
        {
            if (next < cuts.Count && cuts[next] == i)
            {
                fields[field++] = current.Trim();
                current = pieces[i];
                next++;
            }
            else
            {
                current += "," + pieces[i];
            }
        }
        fields[field] = current.Trim();
        return fields;
    }

    // усі способи вибрати count елементів зі списку, у порядку зростання індексів
    private static IEnumerable<List<int>> Combinations(List<int> items, int count)
    {
        var indexes = new int[count];
        for (int i = 0; i < count; i++) indexes[i] = i;

        while (true)
        {
            yield return indexes.Select(i => items[i]).ToList();

            int pos = count - 1;
            while (pos >= 0 && indexes[pos] == items.Count - count + pos) pos--;
            if (pos < 0) yield break;

            indexes[pos]++;
            for (int i = pos + 1; i < count; i++) indexes[i] = indexes[i - 1] + 1;
        }
    }
}
