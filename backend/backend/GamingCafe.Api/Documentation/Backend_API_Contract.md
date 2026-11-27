# Backend API Contract

## 1. Device Registration Endpoint
- **URL**: `POST /api/devices/register`
- **Anonymous Access**: Allowed
- **Request Body**:
```json
{
  "localDeviceId": "542123a5-4081-48e2-b781-7ea226599fee",
  "deviceCode": "PC-542123A5",
  "displayName": "Station-01",
  "hostname": "DESKTOP-STATION01",
  "hardwareFingerprintHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "agentVersion": "1.0.0"
}
```
- **Response** (`201 Created` or `200 OK` on retry):
```json
{
  "id": "28561e93-1506-4c53-9a72-6ca5046c159d",
  "deviceCode": "PC-542123A5",
  "displayName": "Station-01",
  "registrationStatus": "PENDING",
  "message": "Device registered successfully and is awaiting admin approval."
}
```

## 2. Device Status Endpoint
- **URL**: `GET /api/devices/{id}/status`
- **Anonymous Access**: Allowed
- **Response** (`200 OK`):
```json
{
  "id": "28561e93-1506-4c53-9a72-6ca5046c159d",
  "deviceCode": "PC-542123A5",
  "displayName": "Station-01",
  "registrationStatus": "APPROVED",
  "isOnline": false,
  "lastSeenAt": null,
  "approvedAt": "2026-11-27T14:40:00Z"
}
```

## 3. Claim Secret Endpoint
- **URL**: `POST /api/devices/{id}/claim-secret`
- **Anonymous Access**: Allowed (validates `localDeviceId` ownership proof)
- **Request Body**:
```json
{
  "localDeviceId": "542123a5-4081-48e2-b781-7ea226599fee"
}
```
- **Response** (`200 OK`):
```json
{
  "deviceId": "28561e93-1506-4c53-9a72-6ca5046c159d",
  "enrollmentSecret": "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
  "message": "Enrollment secret provisioned successfully."
}
```

## 4. Device Login Endpoint
- **URL**: `POST /api/auth/device/dev-login`
- **Request Body**:
```json
{
  "deviceId": "28561e93-1506-4c53-9a72-6ca5046c159d",
  "enrollmentSecret": "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"
}
```
- **Response** (`200 OK`):
```json
{
  "deviceId": "28561e93-1506-4c53-9a72-6ca5046c159d",
  "accessToken": "eyJhbGciOi...",
  "expiresAt": "2026-11-27T16:00:00Z"
}
```
