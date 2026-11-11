# Assumptions

1. **Student identity:** No login is implemented. The 17-digit birth registration number is sent in the `X-Student-Id` header and is treated as the student's identity.
2. **Hold duration:** `HOLD_DURATION_SECONDS` defaults to 600 seconds.
3. **Automatic expiry:** A hold stops counting toward available seats as soon as `ExpiresAt` passes. Its stored status is updated when a later application request cleans up expired holds.
4. **Concurrency:** Applications for the same class are serialized by locking the corresponding PostgreSQL class row with `SELECT ... FOR UPDATE`.
5. **One active application per student per class:** Enforced in application logic and with a partial unique index on `(ClassId, StudentId)` for HELD and CONFIRMED statuses.
6. **Confirm is idempotent:** Confirming an already-confirmed application returns success and does not count the seat twice.
7. **Confirm after expiry:** An expired hold returns `409 HOLD_EXPIRED`.
8. **Cancel:** Only HELD applications can be cancelled. Confirmed admissions cannot be cancelled.
9. **Other students' holds:** Another student's hold is returned as not found to prevent viewing or changing another student's application.
10. **Shifts:** Each shift is represented as a separate class with its own seat count.
11. **Class names:** Class names do not need to be unique.
12. **Payment:** The “ভর্তি ফি দিন” action simulates successful payment by calling the confirmation endpoint. No real payment gateway is integrated.
13. **Database schema:** The schema is created at startup using EF Core `EnsureCreated`. Schema changes after initial creation require a database reset or a migration-based upgrade.
14. **Errors:** Validation and business-rule errors return structured 4xx responses with an error code and Bengali message.
15. **Frontend updates:** The frontend polls relevant endpoints every three seconds. The student number is stored in browser localStorage so it survives a page refresh.
16. **Test database:** Docker initializes a separate `school_admission_test` database for integration tests so test records do not mix with normal application data.
