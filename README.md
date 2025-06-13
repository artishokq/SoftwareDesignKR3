# Контрольная работа №3 КПО - Асинхронное межсервисное взаимодействие
---

Набор из трёх микросервисов на .NET 8 для управления заказами и платежами.

## Состав проекта

1. **ApiGateway**

   * маршрутизация запросов к OrderService и PaymentsService
   * порт: `8080:8080`
   * swagger: [http://localhost:8080/swagger/index.html](http://localhost:8080/swagger/index.html)

2. **OrderService**

   * для заказов, хранение в PostgreSQL + Outbox для Kafka
   * фоновые сервисы: OrderOutboxPublisher (отправка из Outbox), PaymentResultConsumer (обработка результатов платежей)
   * порт: `8081:8080`
   * swagger: [http://localhost:8081/swagger/index.html](http://localhost:8081/swagger/index.html)

3. **PaymentsService**

   * управление счетами (создание, пополнение, получение баланса), хранение в PostgreSQL + Inbox/Outbox для Kafka
   * фоновые сервисы: PaymentProcessor (обработка задач оплаты), OutboxPublisher (отправка результатов платежей)
   * порт: `8082:8080`
   * swagger: [http://localhost:8082/swagger/index.html](http://localhost:8082/swagger/index.html)
     
---

## Архитектура микросервисов

Система состоит из трёх независимых сервисов, каждый отвечает за свою область:

---

### 1. API Gateway

* **ApiGatewayController.cs** — центральная точка входа: принимает все HTTP-запросы от клиента.
* Использует `HttpClientFactory` (настраивается в `Program.cs`) для проксирования запросов к OrderService и PaymentsService.
* Обеспечивает единый URL для взаимодействия с обоими сервисами.
* Порт (в Docker Compose): `8080:8080`
* Swagger: [http://localhost:8080/swagger/index.html](http://localhost:8080/swagger/index.html)

---

### 2. OrderService

* **OrdersController.cs** — контроллер, обрабатывает HTTP-запросы, связанные с заказами (создание, получение списка, получение по ID).
* **OrderService.cs / IOrderService.cs** — бизнес-логика:

  * валидация данных
  * сохранение `Order` в PostgreSQL через EF Core
  * запись задачи в `OrderOutbox` (для дальнейшей отправки в Kafka)
* **OrderDbContext.cs** — контекст EF Core, модели:

  * `Order` (Id, UserId, Amount, Description, Status, CreatedAt)
  * `OrderOutbox` (Id, OrderId, Payload, IsSent, CreatedAt)
* **Фоновые сервисы**:

  1. **OrderOutboxPublisher.cs**: периодически читает из `OrderOutbox` все записи с `IsSent = false`, отправляет их в Kafka (топик `order_tasks_topic`), помечает `IsSent = true`.
  2. **PaymentResultConsumer.cs**: подписывается на Kafka (топик `payment_results_topic`), при получении `{ OrderId, IsSuccess, FailureReason }` меняет `Order.Status` на `FINISHED` или `CANCELLED`.
* Порт (в Docker Compose): `8081:8080`
* Swagger: [http://localhost:8081/swagger/index.html](http://localhost:8081/swagger/index.html)

---

### 3. PaymentsService

* **PaymentsController.cs** — контроллер, обрабатывает HTTP-запросы, связанные со счетами (создание счета, пополнение, получение баланса).
* **AccountService.cs / IAccountService.cs** — бизнес-логика:

  * проверка и создание `Account`
  * изменение `Balance`
  * при пополнении формирование записи в `PaymentOutbox`
* **PaymentDbContext.cs** — контекст EF Core, модели:

  * `Account` (UserId, Balance)
  * `PaymentInbox` (MessageKey, OrderId, UserId, Amount, Processed, CreatedAt)
  * `PaymentOutbox` (Id, OrderId, IsSuccess, Payload, IsSent, CreatedAt)
* **Фоновые сервисы**:

  1. **PaymentProcessor.cs**: подписывается на Kafka (топик `order_tasks_topic`), при получении `{ OrderId, UserId, Amount }` проверяет баланс:

     * если денег достаточно → уменьшает `Balance`, создаёт `PaymentOutbox` с `IsSuccess = true`;
     * если недостаточно → создаёт `PaymentOutbox` с `IsSuccess = false` и `FailureReason = "Insufficient balance"`.
  2. **OutboxPublisher.cs**: периодически читает из `PaymentOutbox` все записи с `IsSent = false`, отправляет их в Kafka (топик `payment_results_topic`), помечает `IsSent = true`.
* Порт (в Docker Compose): `8082:8080`
* Swagger: [http://localhost:8082/swagger/index.html](http://localhost:8082/swagger/index.html)

---

### Основные принципы

* **Разделение ответственности:** ApiGateway, OrderService и PaymentsService решают строго свои задачи.
* **Асинхронная интеграция через Kafka:** шаблон Outbox/Inbox гарантирует надёжную доставку задач и результатов.
* **Масштабируемость и независимость деплоя:** каждый сервис можно разворачивать и масштабировать отдельно.
* **Единый контракт и документация:** все API задокументированы через Swagger и используют JSON.
 

---

## API Endpoints

Все сервисы подняты локально и доступны по следующим портам:

* **API Gateway**: `http://localhost:8080`
* **OrderService**:  `http://localhost:8081`
* **PaymentsService**: `http://localhost:8082`

---

### API Gateway (порт 8080)

* **POST**   `/api/orders`
  — создаёт новый заказ (проксирует на OrderService)

* **GET**    `/api/orders`
  — возвращает список заказов (проксирует на OrderService)

* **GET**    `/api/orders/{orderId}`
  — возвращает информацию о конкретном заказе (проксирует на OrderService)

* **POST**   `/api/payments/accounts/{userId}`
  — создаёт счёт для пользователя `userId` (проксирует на PaymentsService)

* **POST**   `/api/payments/accounts/{userId}/topup`
  — пополняет баланс счёта пользователя (проксирует на PaymentsService)

* **GET**    `/api/payments/accounts/{userId}/balance`
  — возвращает текущий баланс пользователя `userId` (проксирует на PaymentsService)

---

### OrderService (порт 8081)

* **POST**   `/api/orders`
  — сохраняет новый заказ в БД и помещает задачу в Outbox
* **GET**    `/api/orders`
  — возвращает все заказы из БД
* **GET**    `/api/orders/{orderId}`
  — возвращает заказ с идентификатором `orderId`

---

### PaymentsService (порт 8082)

* **POST**   `/api/accounts/{userId}`
  — создаёт новый счёт для пользователя `userId`
* **POST**   `/api/accounts/{userId}/topup`
  — пополняет баланс счёта пользователя `userId`; при этом создаёт запись в PaymentOutbox
* **GET**    `/api/accounts/{userId}/balance`
  — возвращает текущий баланс пользователя `userId`

---

## Запуск в Docker Compose

1. Собрать контейнеры:

```bash
docker compose build
```

2. Запустить весь стек в фоне:

```bash
docker compose up -d
```

  Иногда бывает, что с первого раза не получается запустить, нужно просто еще раз эту же команду отправить.

3. Swagger UI будет доступен по адресам:

* Gateway:       [http://localhost:8080/swagger/index.html](http://localhost:8080/swagger/index.html)
* OrderService:  [http://localhost:8081/swagger/index.html](http://localhost:8081/swagger/index.html)
* PaymentsService: [http://localhost:8082/swagger/index.html](http://localhost:8082/swagger/index.html)

4. Пример:

Заходим на [http://localhost:8080/swagger/index.html](http://localhost:8080/swagger/index.html)

   Для примера возьмем id: 
```bash
3fa85f64-5717-4562-b3fc-2c963f66afa1
```
Сначала мы должны создать аккаунт, для этого как раз нам пригодится id.
ищем /api/v1/accounts/{userId}

Далее мы можем пополнить аккаунт на N денег.
ищем /api/v1/accounts/{userId}/topup

Дальше мы можем создать заказ.
ищем /api/v1/orders

Можем посмотреть баланс. ищем /api/v1/accounts/{userId}/balance

Можем посмотреть историю заказов. ищем /api/v1/orders

(статус заказа сразу после его размещения будет 0, т.е. "NEW", далее мы можем посмотреть список всех заказов и там должен обновиться статус, но он может поменяться не сразу, а через пару секунд.)


---

### Юнит-тесты
Чтобы проверить покрытие, запустите тестовые проекты отдельно:

```bash
Tests/APIGatewayTests
Tests/OrderServiceTests
Tests/PaymentServiceTests
```

- Покрытие тестами более 65%
<img width="481" alt="tests" src="https://github.com/user-attachments/assets/5f68a72b-8ef0-4de2-bee4-70a9163be1a8" />
