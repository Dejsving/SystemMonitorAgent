# SystemMonitorAgent

Windows-приложение **System Monitor Agent** на .NET 8.0. Приложение может работать как Windows Service и в консольном режиме, периодически собирает информацию о машине и отправляет её на HTTP API.

## Архитектура

Приложение спроектировано для стабильной, отказоустойчивой работы в качестве Windows Service, с поддержкой Graceful Shutdown и интеграцией как с файловыми логами, так и с Windows Event Log.

- **Program.cs** — настройка Host, конфигурации, внедрения зависимостей, логирования (включая EventLog) и режима Windows Service.
- **Worker.cs** — основной фоновый цикл: сбор данных через интервалы времени и их отправка на API. Перед сбором также пытается отправить данные, накопленные в Retry-очереди при предыдущих неудачных отправках. Поддерживает отмену через `CancellationToken` (Graceful Shutdown).
- **PayloadQueue.cs** — потокобезопасная in-memory очередь (`Channel<T>`) для хранения собранных метрик (Retry-очередь).
- **SystemSnapshotCollector.cs** — сбор системной информации: uptime, CPU, RAM, диски, запущенные процессы.
- **ApiClient.cs** — HTTP-клиент для POST-запросов на API с поддержкой таймаутов.
- **AgentHealthState.cs**, **HealthcheckService.cs** и **StartupWarningLogger.cs** — валидация конфигурации, отслеживание внутренних состояний (Healthcheck).
- **FileLoggerProvider.cs** — кастомный провайдер для записи логов работы (запуск, остановка, ошибки, отправка данных) в текстовый файл.
- **SystemMonitorReceiver** (отдельный проект) — консольное ASP.NET Core приложение-приёмник для тестирования получения payload.

## Сборка проекта

Все команды предполагают выполнение из корня репозитория.

```bash
dotnet restore src/SystemMonitorAgent/SystemMonitorAgent.csproj
dotnet build src/SystemMonitorAgent/SystemMonitorAgent.csproj -c Release
```

## Конфигурация

Основной конфигурационный файл:

- во время разработки: `src/SystemMonitorAgent/appsettings.json`
- после `dotnet publish`: `appsettings.json` рядом с `SystemMonitorAgent.exe`

Параметры секции `Agent`:

- `ApiUrl` — адрес HTTP API для POST-запросов.
- `CollectionIntervalSeconds` — интервал сбора данных.
- `RequiredProcesses` — список процессов для проверки.
- `LogFilePath` — путь к файлу лога.
- `HttpTimeoutSeconds` — timeout HTTP-запроса.
- `EventLogLevel` — минимальный уровень логгирования в Windows Event Log.
- `RetryQueueMaxItems` — максимальное количество записей в in-memory очереди перед тем как новые при ошибке отправки начнут отбрасываться.

Пример конфигурации находится в:

- `src/SystemMonitorAgent/appsettings.json`
- `src/SystemMonitorAgent/appsettings.example.json`

### Как изменить конфигурацию

1. Остановить службу: `sc.exe stop SystemMonitorAgent`
2. Изменить `appsettings.json` рядом с опубликованным `SystemMonitorAgent.exe`
3. При необходимости скорректировать:
   - `Agent:ApiUrl`
   - `Agent:CollectionIntervalSeconds`
   - `Agent:RequiredProcesses`
   - `Agent:LogFilePath`
   - `Agent:HttpTimeoutSeconds`
   - `Agent:EventLogLevel`
   - `Agent:RetryQueueMaxItems`
4. Запустить службу снова: `sc.exe start SystemMonitorAgent`

## Установка Windows Service

**Внимание:** Для установки, управления и удаления службы необходимо запускать консоль (PowerShell или cmd) от имени **Администратора**.

1. Опубликовать приложение:

   ```powershell
   dotnet publish src/SystemMonitorAgent/SystemMonitorAgent.csproj -c Release -r win-x64 --self-contained false
   ```

2. Установить службу с помощью готового PowerShell скрипта:

   ```powershell
   .\scripts\Install-Service.ps1
   ```
   *Скрипт автоматически найдет опубликованный .exe файл, создаст службу и запустит её.*

*Альтернативный способ (через sc.exe вручную, если исполняемый файл был скопирован в отдельную папку, например C:\Services\SystemMonitorAgent):*
`sc.exe create SystemMonitorAgent binPath= "C:\Services\SystemMonitorAgent\SystemMonitorAgent.exe" start= auto`

## Запуск службы

```powershell
sc.exe start SystemMonitorAgent
```

## Остановка службы

```powershell
sc.exe stop SystemMonitorAgent
```

## Удаление службы

Службу можно остановить и удалить с помощью скрипта:

```powershell
.\scripts\Uninstall-Service.ps1
```

*Альтернативный способ (вручную):*
`sc.exe delete SystemMonitorAgent`

## Запуск в консольном режиме

```bash
dotnet run --project src/SystemMonitorAgent/SystemMonitorAgent.csproj
```

## Тестовый принимающий сервис (консольное приложение)

```bash
dotnet run --project src/SystemMonitorReceiver/SystemMonitorReceiver.csproj
```

По умолчанию сервис слушает `http://localhost:5000` и принимает POST-запросы на `http://localhost:5000/api/system-monitor`.

## Как проверить работу приложения

1. Запустить `SystemMonitorReceiver` в консольном режиме.
2. Убедиться, что в `src/SystemMonitorAgent/appsettings.json` значение `Agent:ApiUrl` равно `http://localhost:5000/api/system-monitor`.
3. Запустить `SystemMonitorAgent` в консольном режиме или как Windows Service.
4. Проверить консоль `SystemMonitorReceiver` — там должен печататься полученный JSON payload.
5. Проверить файл логов агента — там должны появляться записи об успешной отправке данных или ошибках.

## Где находятся логи

По умолчанию: `logs/system-monitor-agent.log` относительно каталога опубликованного приложения. Путь можно изменить через `Agent:LogFilePath`.

## Пример JSON, который отправляется на API

Пример находится в файле `examples/sample-payload.json`.
