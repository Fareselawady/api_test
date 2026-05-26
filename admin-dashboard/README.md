# Admin Dashboard

Standalone React admin dashboard for the existing `api_test` backend.

## Run

```bash
npm install
npm run dev
```

Set `VITE_API_BASE_URL` if the backend is not running at `http://localhost:5135`.

```bash
VITE_API_BASE_URL=https://localhost:7236
```

## Backend APIs Used

- `POST /api/Auth/login`
- `GET /api/Auth/admin-only`
- `GET /api/Users/all-users`
- `POST /api/Users`
- `PUT /api/Users/{id}`
- `DELETE /api/Users/{userId}`
- `GET /api/Medications/all`
- `POST /api/Medications/add`
- `DELETE /api/Medications/delete/{id}`
- `GET /api/MedIngredients/all`
- `GET /api/MedIngredients/by-med/{medName}`
- `POST /api/MedIngredients/add`
- `GET /api/admin/support`
- `POST /api/admin/support/{id}/reply`
- `PATCH /api/admin/support/{id}/status`
- `GET /api/Surveys`
- `GET /api/Surveys/user/{userId}`
- `POST /api/Surveys/reply/{userId}`
- `GET /api/users/{userId}/alerts`
- `GET /api/users/{userId}/alerts/unread-count`
- `PATCH /api/users/{userId}/alerts/read-all`
- `DELETE /api/alerts/cleanup`

## Missing Backend Endpoints Needed

These dashboard sections are present, but full functionality needs backend support:

- Medication editing:
  - `PUT /api/Medications/{id}`
  - Body: `{ "tradeName": "string", "description": "string", "dosageForm": "string", "imageUrl": "string" }`
  - Admin only.

- Admin premium subscription management:
  - `GET /api/admin/premium/subscriptions`
  - Returns users with `isPremium`, `premiumStartDate`, `premiumEndDate`, and remaining days.
  - `POST /api/admin/users/{userId}/premium/activate`
  - Body: `{ "plan": "Month" | "ThreeMonths" | "Year" }`
  - `POST /api/admin/users/{userId}/premium/cancel`
  - Admin only.

- Global admin alert monitoring:
  - `GET /api/admin/alerts?type=&isRead=&userId=`
  - Returns alerts across users for monitoring without selecting one user at a time.

- OCR scan history and chatbot monitoring:
  - No persistence endpoint exists today. Add scan/conversation log tables and expose admin list endpoints if monitoring is required.
