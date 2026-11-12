# School Admission System

An online admission system for a school. Each shift of a class is a separate class with its own seat count. Students apply for a seat, which is held for a limited time. The seat is confirmed when the admission fee is paid within that window. Otherwise, the seat is released automatically.

## Tech Stack

- **Backend:** C# / .NET 8 Web API, Entity Framework Core 8
- **Database:** PostgreSQL 16
- **Frontend:** React 18, JavaScript, Vite
- **Containers:** Docker, Docker Compose
- **Tests:** xUnit with integration tests against PostgreSQL

## Requirements

- Docker Desktop (Windows, macOS) or Docker Engine with the Compose plugin (Linux)

No other software is required.

## Project Structure

```
school-admission-system/
├── docker-compose.yml        Runs PostgreSQL, backend and frontend
├── README.md
├── ASSUMPTIONS.md            Decisions made where the brief was unclear
├── AI_USAGE.md               Use of AI tools
├── backend/
│   ├── Dockerfile
│   ├── SchoolAdmission.Api/
│   │   ├── Program.cs        Endpoints and startup
│   │   ├── Models.cs         Entities and API data shapes
│   │   └── Data/AppDbContext.cs
│   └── SchoolAdmission.Tests/
│       ├── ApiFactory.cs
│       └── HoldApiTests.cs
└── frontend/
    ├── Dockerfile
    └── src/
        ├── App.jsx           Layout and navigation
        ├── pages.jsx         Application screens
        ├── api.js            Backend calls
        └── utils.js
```

## Running the Project

1. Start Docker Desktop and wait until it reports that the engine is running.
2. Clone the repository and open it in a terminal:

   ```bash
   git clone https://github.com/SabbirNiyaz/school-admission-system.git
   cd school-admission-system
   ```

3. Start the system:

   ```bash
   docker compose up --build
   ```

4. Open the application:

   | Service  | Address                 |
   |----------|-------------------------|
   | Frontend | http://localhost:5173   |
   | Backend  | http://localhost:8080   |

5. To stop the system, press `Ctrl+C`, then run:

   ```bash
   docker compose down
   ```

To run in the background, use `docker compose up --build -d`. To delete all data, use `docker compose down -v`.

## Running the Tests

```bash
docker compose --profile test run --rm tests
```

## Configuration

| Variable | Service | Default | Purpose |
|---|---|---|---|
| `ConnectionStrings__Default` | backend | Database service in Compose | PostgreSQL connection string |
| `HOLD_DURATION_SECONDS` | backend | `600` | Duration a seat is held |
| `VITE_API_URL` | frontend | `http://localhost:8080` | Backend URL used by the browser |

## Business Rules

- A student is identified by a 17-digit birth registration number, sent in the `X-Student-Id` header. There is no login.
- A student can hold only one active application per class.
- A seat is free when its hold has expired, without any API call.
- The number of held and confirmed seats never exceeds the total seats, even under concurrent requests.
- Confirming an application twice does not count the seat twice.
- A student can cancel only their own held application.

## Application Status

| Status      | Meaning                                  | Holds a seat |
|-------------|------------------------------------------|--------------|
| `HELD`      | Seat reserved, fee not yet paid          | Until expiry |
| `CONFIRMED` | Admission confirmed after fee payment    | Yes          |
| `EXPIRED`   | Time limit passed before payment         | No           |
| `CANCELLED` | Cancelled by the student                 | No           |

## API Reference

All requests and responses use JSON. Errors use the same format:

```json
{ "code": "SEAT_FULL", "message": "Bengali message for the user" }
```

| CRUD   | Method | Endpoint                     | Description                      | Student header |
|--------|--------|------------------------------|----------------------------------|----------------|
| Create | POST   | `/classes`                   | Create a class (shift)           | No             |
| Read   | GET    | `/classes`                   | List all classes                 | No             |
| Read   | GET    | `/classes/{classId}`         | Get one class                    | No             |
| Delete | DELETE | `/classes/{classId}`         | Delete a class with no applications (additional) | No |
| Create | POST   | `/classes/{classId}/holds`   | Apply for a seat                 | Yes            |
| Read   | GET    | `/holds/{holdId}`            | Get application status           | No             |
| Update | POST   | `/holds/{holdId}/confirm`    | Pay fee and confirm admission    | Yes            |
| Delete | DELETE | `/holds/{holdId}`            | Cancel own held application      | Yes            |
| Read   | GET    | `/me/holds`                  | List the student's applications  | Yes            |

### Create a Class

`POST /classes`

Request:

```json
{
  "name": "Class 6 - Morning Shift",
  "totalSeats": 120
}
```

Response, `201 Created`:

```json
{
  "id": "c_911cca92bdb54035a7da203c947d23bb",
  "name": "Class 6 - Morning Shift",
  "totalSeats": 120
}
```

### List Classes

`GET /classes`

Response, `200 OK`:

```json
[
  {
    "id": "c_911cca92bdb54035a7da203c947d23bb",
    "name": "Class 6 - Morning Shift",
    "totalSeats": 120,
    "heldSeats": 15,
    "confirmedSeats": 80,
    "availableSeats": 25
  }
]
```

### Get a Class

`GET /classes/{classId}`

Response, `200 OK`:

```json
{
  "id": "c_911cca92bdb54035a7da203c947d23bb",
  "name": "Class 6 - Morning Shift",
  "totalSeats": 120,
  "heldSeats": 15,
  "confirmedSeats": 80,
  "availableSeats": 25
}
```

Response, `404 Not Found`:

```json
{ "code": "CLASS_NOT_FOUND", "message": "এই ক্লাসটি পাওয়া যায়নি।" }
```

### Delete a Class

`DELETE /classes/{classId}`

This endpoint is an additional admin operation. It succeeds only if the class has no applications.

Response, `200 OK`:

```json
{
  "success": true,
  "message": "Class deleted successfully.",
  "data": {
    "id": "c_911cca92bdb54035a7da203c947d23bb",
    "name": "Class 6 - Morning Shift"
  }
}
```

Response, `409 Conflict`:

```json
{ "code": "CLASS_HAS_RESERVATIONS", "message": "এই ক্লাসে আবেদন বা ভর্তি সংক্রান্ত রেকর্ড রয়েছে। তাই ক্লাসটি মুছে ফেলা যাবে না।" }
```

### Apply for a Seat

`POST /classes/{classId}/holds`

Headers: `X-Student-Id: 20152691234567890`

Response, `201 Created`:

```json
{
  "holdId": "h_a1b2c3d4e5f60718293a4b5c6d7e8f90",
  "classId": "c_911cca92bdb54035a7da203c947d23bb",
  "studentId": "20152691234567890",
  "status": "HELD",
  "expiresAt": "2026-11-10T04:10:00Z"
}
```

Possible errors: `409 SEAT_FULL`, `409 ALREADY_APPLIED`, `400 INVALID_STUDENT_ID`, `404 CLASS_NOT_FOUND`.

### Get an Application

`GET /holds/{holdId}`

Response, `200 OK`:

```json
{
  "holdId": "h_a1b2c3d4e5f60718293a4b5c6d7e8f90",
  "classId": "c_911cca92bdb54035a7da203c947d23bb",
  "status": "EXPIRED",
  "expiresAt": "2026-11-10T04:10:00Z"
}
```

Status is calculated at request time, so an expired hold shows `EXPIRED` without any background process.

### Confirm an Application

`POST /holds/{holdId}/confirm`

Headers: `X-Student-Id: 20152691234567890`

Response, `200 OK`:

```json
{
  "holdId": "h_a1b2c3d4e5f60718293a4b5c6d7e8f90",
  "status": "CONFIRMED"
}
```

Possible errors: `409 HOLD_EXPIRED`, `409 HOLD_NOT_ACTIVE`, `404 HOLD_NOT_FOUND`.

### Cancel an Application

`DELETE /holds/{holdId}`

Headers: `X-Student-Id: 20152691234567890`

Response, `204 No Content`: empty body.

Possible errors: `409 HOLD_NOT_CANCELLABLE`, `404 HOLD_NOT_FOUND`.

### List My Applications

`GET /me/holds`

Headers: `X-Student-Id: 20152691234567890`

Response, `200 OK`:

```json
[
  {
    "holdId": "h_a1b2c3d4e5f60718293a4b5c6d7e8f90",
    "classId": "c_911cca92bdb54035a7da203c947d23bb",
    "className": "Class 6 - Morning Shift",
    "status": "CONFIRMED",
    "expiresAt": "2026-11-10T04:10:00Z"
  }
]
```

## HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | Success |
| 201 | Created |
| 204 | Deleted or cancelled, no content |
| 400 | Invalid input or invalid student ID |
| 404 | Resource not found |
| 409 | Business rule violation |
| 500 | Unexpected server error, logged on the server |

## Database

The backend connects to PostgreSQL through `ConnectionStrings__Default`. Inside Docker Compose, the host is `db`. Tables are created automatically on startup. Data is stored in the Docker volume `pgdata` and survives restarts.

Inspect the data:

```bash
docker compose exec db psql -U postgres -d school_admission -c 'SELECT * FROM "Classes";'
```

## Troubleshooting

| Problem | Solution |
|---|---|
| Port already in use | Stop the program using port 5173, 8080 or 5432, or change the port in `docker-compose.yml`. |
| Backend keeps restarting | Run `docker compose logs backend --tail 100` and read the first exception. |
| Old code still running | Run `docker compose build --no-cache backend`, then `docker compose up -d backend`. |
| Schema is outdated | Run `docker compose down -v`, then `docker compose up --build`. This deletes all data. |
