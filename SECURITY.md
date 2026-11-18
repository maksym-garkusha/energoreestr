# Безпека

## Як повідомити про вразливість

Не відкривайте публічний issue. На вкладці [Security](https://github.com/maksym-garkusha/energoreestr/security) є кнопка «Report a vulnerability»: повідомлення через неї бачить тільки власник репозиторію.

Відповідь протягом 7 днів. Винагород за знахідки немає, це навчальний проєкт без бюджету.

## Що в межах

- сторінка пошуку на [maksym-garkusha.github.io/energoreestr](https://maksym-garkusha.github.io/energoreestr/) і `data.json`, який вона завантажує;
- код у цьому репозиторії: імпорт і розбір файлів з data.gov.ua, HTTP API (запускається локально), workflows GitHub Actions.

Сюди ж, якщо на сторінці або в `data.json` трапився запис фізичної особи-підприємця: таких записів там бути не повинно.

Поза межами портал data.gov.ua, самі реєстри та платформи GitHub. Неточність у розборі файлу чи в даних на сторінці це не вразливість, про таке можна писати у звичайний issue.

Як перевірити, що `data.json` зібрано workflow цього репозиторію, описано в [README](README.md#як-влаштовано).

## In English

Please do not open a public issue. Use the "Report a vulnerability" button on the Security tab; only the repository owner sees the report. In scope: the search page on GitHub Pages with its `data.json`, and the code here (import and parsing, the local HTTP API, the Actions workflows). Out of scope: data.gov.ua, the registers themselves and GitHub's own platforms. Expect a reply within 7 days. There is no bug bounty.
