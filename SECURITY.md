# Безпека

## Як повідомити про вразливість

Не відкривайте публічний issue. На вкладці [Security and quality](https://github.com/maksym-garkusha/energoreestr/security) є кнопка «Report a vulnerability»: повідомлення через неї бачить тільки власник репозиторію.

Відповідь протягом 7 днів. Винагород за знахідки немає, це навчальний проєкт без бюджету.

## Що в межах

- сторінка пошуку на [maksym-garkusha.github.io/energoreestr](https://maksym-garkusha.github.io/energoreestr/) і `data.json`, який вона завантажує;
- сторінка перевірки файлу [maksym-garkusha.github.io/energoreestr/check](https://maksym-garkusha.github.io/energoreestr/check/) і команда `check`;
- код у цьому репозиторії: імпорт і розбір файлів з data.gov.ua, HTTP API (запускається локально), workflows GitHub Actions.

Сюди ж, якщо на сторінці, в `data.json`, у звіті чи у відповіді API трапилися ім'я або код фізичної особи-підприємця: їх там бути не повинно.

Для сторінки перевірки вразливість це будь-що з такого: вона робить мережевий запит після завантаження, виконує щось із вмісту файлу (скрипт, розмітку, посилання), показує на екрані чи пише в збережений звіт значення з рядка, позначеного як персональні дані. Те саме для команди `check`, якщо вона щось записує на диск або виводить такі значення. Зависання вкладки на дуже великому файлі вразливістю не вважаємо, про таке краще написати у звичайний issue.

Поза межами портал data.gov.ua, самі реєстри та платформи GitHub. Неточність у розборі файлу чи в даних на сторінці це не вразливість, про таке можна писати у звичайний issue.

Як перевірити, що `data.json` зібрано workflow цього репозиторію, описано в [README](README.md#як-влаштовано).

## In English

Please do not open a public issue. Use the "Report a vulnerability" button on the Security and quality tab; only the repository owner sees the report. In scope: the search page on GitHub Pages with its `data.json`, the file check page and the `check` command, and the code here (import and parsing, the local HTTP API, the Actions workflows). For the file check page, a vulnerability is any network request after the page has loaded, anything from the file being executed as script or markup, or a value from a row flagged as personal data appearing on screen or in the saved report; the same goes for the command writing to disk or printing such values. A tab that hangs on a very large file is not a vulnerability; please open an ordinary issue for that. Out of scope: data.gov.ua, the registers themselves and GitHub's own platforms. Expect a reply within 7 days. There is no bug bounty.
