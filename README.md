# FuelErp — облік палива та мастильних матеріалів
Лабораторні роботи виконали разом: Соня (https://github.com/sofaivani4eva-png) та Ліза (https://github.com/martiskoliza-rgb).

Підсумковий модуль мобільного ERP-застосунку залізничної компанії (.NET MAUI 8, C#, MVVM).
Лабораторна робота №9, варіант 7: **«Облік палива та мастильних матеріалів»**.

* Сутності: **Заправка** (`Refueling`), **Резервуар** (`FuelTank`), **ТранспортнийЗасіб** (`Vehicle`).
* Ролі: **Оператор АЗС** (`StationOperator`) та **Бухгалтер** (`Accountant`).
* Апаратні функції: **камера** (фото чека/лічильника) і **геолокація** точки заправки (+ відкриття точки на карті, індикатор мережі).
* Платформи: Windows (`net8.0-windows10.0.19041.0`) і Android (`net8.0-android`).

## Що вміє застосунок

* Вхід за логіном і паролем; токен (JWT) зберігається в SecureStorage; на Windows доступний повторний вхід через Windows Hello.
* Список заправок з пошуком, фільтром за видом пального та підсумком (літри/сума) → картка заправки → форма створення/редагування з валідацією.
* Реєстрація заправки зменшує залишок у вибраному резервуарі (в одній транзакції); якщо палива не вистачає — запис не зберігається.
* Резервуари з кольоровою індикацією заповненості, довідник транспортних засобів, звіт для бухгалтера.
* Офлайн-черга `SyncQueue`: зміни зберігаються локально й відправляються на тестовий сервер; конфлікти — «останній запис виграє» (за `UpdatedAt`).
* Ім'я водія шифрується (AES) у базі даних; ключ лежить у SecureStorage.
* Світла/темна/системна тема зі збереженням вибору, deep link `fuelerp://refueling/{id}`, збереження незавершеної форми при згортанні.
* Serilog (консоль + файл із ротацією), глобальний обробник винятків, екран перегляду логів, Sentry.

## Тестові облікові записи

| Логін | Пароль | Роль |
|-------|--------|------|
| `operator` | `Operator#2024` | Оператор АЗС |
| `accountant` | `Accountant#2024` | Бухгалтер |

Облікові записи існують лише на тестовому сервері (`stub/FuelErp.StubApi`), у застосунку паролів немає.

## Інструкція запуску

Потрібно: Windows 10/11, Visual Studio 2022 17.8+ з робочим навантаженням «.NET Multi-platform App UI development», .NET SDK 8.

1. Клонувати репозиторій і перейти на потрібну гілку: `git checkout lab9`.
2. Запустити тестовий сервер (окреме вікно PowerShell, залишити відкритим):

   ```powershell
   dotnet run --project stub/FuelErp.StubApi
   ```

3. Відкрити `FuelErp.sln` у Visual Studio, вибрати проєкт `FuelErp.App`, ціль **Windows Machine**, натиснути **F5**.
4. Увійти як `operator` або `accountant` (таблиця вище).

Android: у Visual Studio вибрати Android-емулятор і виконати збирання з параметром `-p:IncludeAndroid=true`
(`dotnet build FuelErp.App -f net8.0-android -p:IncludeAndroid=true`; потрібне навантаження `maui-android`).
З емулятора адреса `localhost` вказує на сам емулятор, тому для зв'язку з сервером на комп'ютері використовується `10.0.2.2`.

Тести та покриття:

```powershell
dotnet test FuelErp.Core.Tests --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

UI-тести Appium (за замовчуванням пропускаються) описано у `FuelErp.UiTests/README.md`.

## Архітектура

```
FuelErp.sln
├── FuelErp.Core/            net8.0, без жодних залежностей від Microsoft.Maui.*
│   ├── Models/              Refueling, FuelTank, Vehicle, FuelType, UserRole, RefuelingDraft, ...
│   ├── Data/                AppDatabase (SQLite, PRAGMA user_version), записи таблиць, репозиторії + інтерфейси,
│   │                        SyncQueueItem / SyncQueueWriter, FuelDataSeeder
│   ├── Services/            інтерфейси (INavigationService, IDialogService, IPhotoService, IDeviceLocationService, ...)
│   │                        та чиста логіка: AuthService, SessionService, AesEncryptionService, JwtParser, AppStateService
│   └── ViewModels/          Refuelings / RefuelingDetail / RefuelingEdit / Tanks / Vehicles / Report / Login / Users / Settings / Logs
├── FuelErp.App/             MAUI: Views (XAML без бізнес-логіки), Converters, платформні реалізації інтерфейсів,
│                            Platforms (AndroidManifest, Package.appxmanifest), MauiProgram.cs (DI, Serilog, Sentry)
├── FuelErp.Core.Tests/      xUnit + Moq + FluentAssertions: модульні та інтеграційні тести (SQLite у тимчасовому файлі)
├── FuelErp.UiTests/         Appium: сценарії «вхід за двома ролями» та «додавання транспортного засобу»
├── stub/FuelErp.StubApi/    тестовий ASP.NET Core API (вхід, JWT, перелік персоналу, синхронізація)
└── .github/workflows/ci.yml
```

Правило залежностей: `App → Core`, `Core.Tests → Core`. Усе, що торкається пристрою (Shell, діалоги, камера, геолокація,
SecureStorage, мережа, тема), прихована за інтерфейсами в Core; ViewModel не викликає `Shell.Current` чи статичні API пристрою.

### Час життя сервісів у DI

| Lifetime | Що саме | Чому |
|----------|---------|------|
| Singleton | `AppStateService`, `SessionService`, `AuthService`, `AppDatabase`, репозиторії, `SyncService`, `AesEncryptionService`, платформні сервіси (навігація, діалоги, камера, геолокація, налаштування, тема) | Спільний стан сеансу, одне підключення до БД, відсутність повторного створення |
| Transient | усі ViewModel та сторінки | Кожне відкриття екрана має чистий стан; дані не «протікають» між відкриттями |

## ER-схема бази даних

```
┌────────────────────┐        ┌────────────────────────┐        ┌────────────────────┐
│ Vehicles           │ 1    * │ Refuelings             │ *    1 │ FuelTanks          │
├────────────────────┤────────├────────────────────────┤────────├────────────────────┤
│ Id (PK)            │        │ Id (PK)                │        │ Id (PK)            │
│ PlateNumber (UQ)   │        │ VehicleId (FK, index)  │        │ Name               │
│ Model              │        │ TankId (FK, index)     │        │ FuelType           │
│ FuelType           │        │ DriverNameEncrypted    │        │ CapacityLiters     │
│ UpdatedAt          │        │ Liters, PricePerLiter  │        │ CurrentLiters      │
└────────────────────┘        │ RefueledAt             │        │ UpdatedAt          │
                              │ ReceiptPhotoPath       │        └────────────────────┘
                              │ Latitude, Longitude    │
                              │ OperatorName, Notes    │        ┌────────────────────┐
                              │ UpdatedAt              │        │ SyncQueue          │
                              └────────────────────────┘        ├────────────────────┤
                                                                │ Id, EntityType     │
                                                                │ Operation          │
                                                                │ PayloadJson        │
                                                                │ CreatedAt          │
                                                                └────────────────────┘
```

* Зв'язки: одна заправка належить одному транспортному засобу й одному резервуару; транспортний засіб і резервуар мають багато заправок.
* Версія схеми зберігається в `PRAGMA user_version` (поточна — 2). Демонстраційна міграція v1 → v2 додає до заправок поле `Notes`.
* `SyncQueue` — черга офлайн-змін; запис у чергу виконується в тій самій транзакції, що й зміна даних.

## Екрани та маршрути

| Екран | Маршрут Shell | ViewModel |
|-------|---------------|-----------|
| Вхід | `//LoginPage` (початковий) | `LoginViewModel` |
| Список заправок | `MainPage` | `RefuelingsViewModel` |
| Картка заправки | `refueling/detail` | `RefuelingDetailViewModel` |
| Форма заправки | `refueling/edit` | `RefuelingEditViewModel` |
| Резервуари | `tanks` | `TanksViewModel` |
| Транспортні засоби | `vehicles` | `VehiclesViewModel` |
| Звіт | `report` | `ReportViewModel` |
| Персонал | `users` | `UsersViewModel` |
| Налаштування | `settings` | `SettingsViewModel` |
| Журнал | `logs` | `LogsViewModel` |

Об'єкт передається між сторінками через `IQueryAttributable` (сторінка пересилає параметри у ViewModel через `IQueryReceiver`).
Після збереження форма повертає збережений запис у список через `INavigationService.GoBackAsync` (параметр `SavedRefueling`),
а список додатково перезавантажується у `OnAppearing` сторінки.

## Матриця доступу «роль — екран — дія»

| Екран / дія | Оператор АЗС | Бухгалтер |
|-------------|:------------:|:---------:|
| Перегляд списку заправок, пошук, фільтр | ✔ | ✔ |
| Картка заправки, відкриття точки на карті | ✔ | ✔ |
| Створення та редагування заправки (фото, геолокація) | ✔ | ✘ |
| Видалення заправки | ✘ | ✔ |
| Резервуари, транспортні засоби | ✔ | ✔ |
| Звіт (підсумки за видами пального) | ✘ | ✔ |
| Персонал (`/api/users`) | ✘ | ✔ |
| Синхронізація, налаштування, журнал, вихід | ✔ | ✔ |

Перевірка прав виконується тричі:

1. **UI** — `IsVisible` кнопок прив'язано до `CanCreate` / `CanDelete` / `IsAccountant`.
2. **Команди** — кожна команда перевіряє роль через `ISessionService.IsInRole(...)`, при відмові пише `Warning` у журнал і показує повідомлення.
3. **Сервер** — `/api/users` вимагає роль `Accountant` у JWT (`RequireRole`); підроблений інтерфейс не обійде перевірку на сервері.

## Таблиця відповідності вимогам п. 1.2

| ЛР | Складова | Де реалізовано |
|----|----------|----------------|
| №1 | Чисті Model, ObservableObject, індикація стану | `Core/Models/*`, `FuelTank.FillFraction/LevelState`, `App/Converters/TankLevelColorConverter.cs`, `[ObservableProperty]` у ViewModel |
| №2 | Команди, Shell-маршрути, список → картка → редагування, IQueryAttributable | `AppShell.xaml.cs`, `RefuelingsViewModel`, `RefuelingDetailViewModel`, `RefuelingEditViewModel`, `Core/Services/IQueryReceiver.cs`, `Views/*Page.xaml.cs` |
| №3 | Сервіс стану (singleton), чернетка форми, тема, deep link | `AppStateService`, `RefuelingDraft`, `SettingsViewModel` + `MauiThemeService`, `MainActivity.cs` / `Package.appxmanifest` (схема `fuelerp`) |
| №4 | ≥3 сутності SQLite, репозиторії, версіонування, SyncQueue, шифрування | `Core/Data/*`, `AppDatabase.MigrateAsync`, `SyncQueueWriter`, `SyncService`, `AesEncryptionService` |
| №5 | Serilog, глобальний обробник, екран логів | `App/Services/LoggingConfiguration.cs`, `MauiProgram.cs`, `LogsViewModel`, `LogsPage`, `SentryCrashReporter` |
| №6 | Вхід, токен у SecureStorage, 2 ролі, UI + команди | `LoginViewModel`, `AuthService`, `SessionService`, `JwtParser`, `MauiSecureStoreService`, `CertificatePinValidator`, `WindowsBiometricService` |
| №7 | ≥2 апаратні функції, дозволи, відмова | `PhotoService`, `DeviceLocationService`, `PermissionService`, `MauiMapLauncher`, `MauiNetworkStatusService`, `AndroidManifest.xml` |
| №8 | Ядро в бібліотеці, ≥12 модульних і ≥2 інтеграційних тестів, GitHub Actions | `FuelErp.Core`, `FuelErp.Core.Tests` (178 тестів), `.github/workflows/ci.yml` |

## Тестування та CI

* `FuelErp.Core.Tests`: модульні тести ViewModel і сервісів (Moq замінює платформні сервіси), інтеграційні тести репозиторіїв на справжньому SQLite
  (CRUD, правила залишку, шифрування в таблиці, порядок `SyncQueue`, міграція v1 → v2, початкові дані).
* Покриття рядків ViewModel — понад 90 % (поріг у CI — 60 %).
* Конвеєр GitHub Actions: `test-core` (Ubuntu: тести, звіт покриття, перевірка порогу) → `build-android` (Windows: збирання Android-версії та публікація `.apk` як артефакту).

## Відомі обмеження

* Тестовий сервер працює на `localhost`; для Android-емулятора адресу треба замінити на `10.0.2.2`.
* Інтерфейс (XAML), Windows Hello і Appium-сценарії перевіряються лише запуском на пристрої; автоматично перевіряється ядро (Core).
