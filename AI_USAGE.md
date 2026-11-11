# AI tools used
- Claude & ChatGPT

## Parts where AI helped
- Backend endpoint design, database model, and concurrency approach.
- Integration test structure and test scenarios.
- React screen structure, polling, and countdown behavior.
- Docker Compose configuration and project documentation.

## Mistakes or changes made during review
- The test database needs to exist before integration tests run. A PostgreSQL initialization script is included to create `school_admission`.
- Review and record any additional issues you personally discover while building and testing the project.

## How the project was verified
- [ ] Ran `docker compose up --build`.
- [ ] Tested class creation and class listing in the browser.
- [ ] Tested applying, confirming, and cancelling an application.
- [ ] Tested hold expiry.
- [ ] Ran `docker compose --profile test run --rm tests`.
- [ ] Reviewed the source code and can explain the row lock, expiry logic, and partial unique index.
