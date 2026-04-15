# AURAID XR Emergency Training System

AURAID is a Unity-based XR application for CPR training and emergency-response support.

It combines:
- Real-time sensor input from an ESP32 glove.
- Guided training and emergency coaching logic.
- Voice interaction (STT/TTS client flow).
- Firebase-backed report generation and SMTP email delivery.

---

## 1) Get the Project

**Repository:** [github.com/Shahzaib1711/AURAID](https://github.com/Shahzaib1711/AURAID)

### Option A - Clone with Git (recommended)

```bash
git clone https://github.com/Shahzaib1711/AURAID.git
cd AURAID
```

### Option B - Download ZIP

1. Open the repo URL in browser.
2. Click **Code** -> **Download ZIP**.
3. Extract ZIP.
4. Use extracted folder as the Unity project root.

---

## 2) Full Prerequisites (Install Everything First)

Install these before setup:

1. **Unity Hub** + compatible **Unity Editor** version for this project.
2. **Git** (optional but recommended).
3. **Node.js 20.x** (required by `functions/package.json`).
4. **Firebase CLI**:
   ```bash
   npm install -g firebase-tools
   ```
5. **Firebase project** (in Firebase Console).
6. **Firestore Database** enabled in that Firebase project.
7. **Arduino IDE** (or PlatformIO) for ESP32 flashing.
8. **ESP32 board package** in Arduino IDE.
9. **SMTP account/app password** (for email sending).

> Important: Keep ESP32 and Unity PC on the same Wi-Fi network.

---

## 3) Repository Structure

- `Assets/` - Unity assets, scripts, UI, logic, prefabs.
- `Assets/Prefabs/TrainingMode/TrainingRoot.prefab` - Training root prefab.
- `Assets/Prefabs/EmergencyMode/EmergencyRoot.prefab` - Emergency root prefab.
- `Assets/Scenes/MainMenu.unity` - Main menu scene.
- `Hardware/ESP32_AURAID_Sensors/ESP32_AURAID_Sensors.ino` - ESP32 firmware.
- `functions/` - Firebase Cloud Functions (PDF + email).
- `firebase.json` - Firebase deploy config.
- `.firebaserc` - Firebase project alias mapping.

---

## 4) Unity Setup (Detailed)

1. Open **Unity Hub**.
2. Choose **Add project from disk**.
3. Select this folder (`AURAID` root).
4. Open project with a compatible Unity version.
5. Wait for package restore and script compile to complete.
6. Open scene:
   - `Assets/Scenes/MainMenu.unity`
7. Verify core prefabs are present and linked:
   - `Assets/Prefabs/TrainingMode/TrainingRoot.prefab`
   - `Assets/Prefabs/EmergencyMode/EmergencyRoot.prefab`
8. Check any inspector fields used for:
   - Voice endpoints/keys.
   - Firebase-related URL/config fields (if exposed in inspector).
9. Press **Play** and confirm no immediate missing-reference errors.

If Unity shows package or compile errors:
- Close Unity.
- Reopen through Unity Hub.
- Let package manager finish restoring dependencies.

---

## 5) ESP32 Setup (Detailed)

Firmware file:
- `Hardware/ESP32_AURAID_Sensors/ESP32_AURAID_Sensors.ino`

### Edit placeholders before upload

In firmware, replace:
- `YOUR_WIFI_SSID`
- `YOUR_WIFI_PASSWORD`
- `YOUR_PC_IPV4`

These appear as:
- `const char* ssid = "YOUR_WIFI_SSID";`
- `const char* password = "YOUR_WIFI_PASSWORD";`
- `const char* host = "YOUR_PC_IPV4";`
- `const int port = 5000;`

### Upload steps

1. Connect ESP32 over USB.
2. In Arduino IDE, select:
   - Correct **Board** (ESP32 model).
   - Correct **COM Port**.
3. Upload firmware.
4. Open Serial Monitor (matching baud rate in sketch).
5. Confirm logs show:
   - Wi-Fi connected.
   - Device local IP.
   - Target host/port connection attempts.

### Network requirements

- Unity machine IP must match `host` in firmware.
- Firewall must allow inbound TCP on port `5000` for Unity app.
- ESP32 and PC must be on same LAN.

---

## 6) Firebase Setup (Project + Firestore + Functions)

This project already includes:
- `firebase.json` with functions source set to `functions`.
- `.firebaserc` alias (`AURAID`) currently mapped to `auraid-71d93`.

You can keep existing project or point alias to your own Firebase project.

### 6.1 Login and select Firebase project

From project root:

```bash
firebase login
firebase use --add
```

Choose your Firebase project and map it to alias `AURAID` (or your preferred alias).

### 6.2 Enable Firestore

In Firebase Console:
1. Open your project.
2. Go to **Firestore Database**.
3. Create database (production or test mode per your need).
4. Set region (choose once; cannot be changed later easily).

### 6.3 Install Cloud Function dependencies

From project root:

```bash
cd functions
npm install
```

`functions/package.json` requires:
- Node `20`
- `firebase-functions`
- `firebase-admin`
- `nodemailer`
- `pdf-lib`
- `pdfkit`
- `chartjs-node-canvas`

### 6.4 Configure SMTP (required for email reports)

The function reads:
- `smtp.user` (required)
- `smtp.pass` (required)
- `smtp.host` (optional, defaults to `smtp.gmail.com`)
- `smtp.port` (optional, defaults to `587`)
- `smtp.from` (optional, defaults to `smtp.user`)

Set config:

```bash
firebase functions:config:set smtp.user="your_email@example.com" smtp.pass="your_smtp_password_or_app_password" smtp.host="smtp.gmail.com" smtp.port="587" smtp.from="AURAID Reports <your_email@example.com>"
```

> For Gmail, use an **App Password** (not your normal account password) when 2FA is enabled.

### 6.5 Deploy functions

From project root:

```bash
firebase deploy --only functions
```

If needed, also deploy Firestore rules:

```bash
firebase deploy --only firestore:rules
```

### 6.6 Verify function deployment

```bash
firebase functions:list
```

Then check Firebase Console -> Functions logs for runtime output.

---

## 7) SMTP Provider Notes (Common Working Configs)

### Gmail (TLS STARTTLS)
- Host: `smtp.gmail.com`
- Port: `587`
- Secure mode: false (STARTTLS upgrade)
- Auth: Gmail address + App Password

### Gmail (SSL)
- Host: `smtp.gmail.com`
- Port: `465`
- Secure mode: true

### Outlook/Office365
- Host: `smtp.office365.com`
- Port: `587`

### SendGrid
- Host: `smtp.sendgrid.net`
- Port: `587` or `465`
- User usually: `apikey`
- Pass: SendGrid API key

After changing SMTP config, redeploy functions:

```bash
firebase deploy --only functions
```

---

## 8) End-to-End Run Sequence

1. Start ESP32 and confirm Serial Monitor shows Wi-Fi connected.
2. Run Unity project and open `MainMenu` scene.
3. Enter **Training** mode and run a CPR session.
4. Save/submit report data flow from Unity.
5. Confirm Firestore document is created/updated.
6. Confirm Cloud Function triggers and generates PDF.
7. Confirm report email is sent through SMTP.
8. Test **Emergency** mode live coaching path.

---

## 9) Troubleshooting (Most Common Issues)

### Unity does not receive ESP32 data
- Check `YOUR_PC_IPV4` in firmware.
- Check Wi-Fi subnet match (same network).
- Check Windows firewall for TCP `5000`.
- Check ESP32 serial output for connection failures.

### Functions deployed but email not sent
- Verify `smtp.user` and `smtp.pass` are set correctly.
- Verify SMTP provider allows SMTP auth from your account.
- For Gmail, ensure App Password is used.
- Review Function logs in Firebase Console.

### Firestore writes fail
- Ensure Firestore database is created.
- Check Firebase project selected in CLI (`firebase use`).
- Deploy Firestore rules if needed.

### Node version mismatch
- Use Node 20.x for `functions` commands.

### Firebase CLI command fails
- Re-run `firebase login`.
- Confirm project alias mapping in `.firebaserc`.

---

## 10) Key Scripts / Components

- `Assets/Scripts/Integration/ESP32TestReceiver.cs` - TCP server + CSV parsing.
- `Assets/Scripts/Emergency/CPR/RuleBasedCprAgent.cs` - emergency CPR coaching rules.
- `Assets/Scripts/Training/CPRSessionManager.cs` - CPR session orchestration.
- `Assets/Scripts/Training/TrainingReportBuilder.cs` - report payload construction.
- `Assets/Scripts/Training/RegistrationHandler.cs` - registration validation + flow.
- `Assets/Scripts/Voice/SttClient.cs` - speech-to-text integration.
- `Assets/Scripts/Voice/TtsClient.cs` - text-to-speech integration.
- `Assets/Scripts/UI/UIFlowManager.cs` - major UI flow transitions.

---

## 11) Project Objectives

- Deliver immersive XR CPR learning.
- Provide real-time compression feedback from physical sensors.
- Support multilingual interaction paths.
- Build cloud-backed report generation and email delivery.
- Demonstrate a complete hardware + software + cloud pipeline.

---




