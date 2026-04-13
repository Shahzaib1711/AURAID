#include <Wire.h>
#include <Adafruit_Sensor.h>
#include <Adafruit_BNO055.h>
#include <utility/imumaths.h>
#include <WiFi.h>

// -------------------- WIFI --------------------
const char* ssid = "YOUR_WIFI_SSID";
const char* password = "YOUR_WIFI_PASSWORD";

const char* host = "YOUR_PC_IPV4";  // e.g. 192.168.1.5
const int port = 5000;

WiFiClient client;

// -------------------- Pins --------------------
static const int PIN_FSR[3] = {32, 35, 34};
static const int PIN_SDA = 21;
static const int PIN_SCL = 22;

static const uint8_t BNO_ADDR = 0x28;

// -------------------- Timing --------------------
static const uint32_t SAMPLE_INTERVAL_MS = 10;
static const uint32_t PRINT_INTERVAL_MS = 50;

// -------------------- Thresholds --------------------
static const float CMP_ON = 0.65f;
static const float CMP_OFF = 0.35f;

static const float MOTION_ACCEL_MAX = 8.0f;
static const float TILT_MAX_DEG = 60.0f;
static const int TILT_EULER_INDEX = 2;

// -------------------- Runtime --------------------
Adafruit_BNO055 bno = Adafruit_BNO055(55, BNO_ADDR);
bool bno_ok = false;

uint32_t g_lastSampleMs = 0;
uint32_t g_lastPrintMs = 0;
uint32_t g_lastTcpFailLogMs = 0;

bool g_inCompression = false;
uint32_t g_cmpTotal = 0;

// -------------------- Helpers --------------------
float clamp01(float x) {
  if (x < 0) return 0;
  if (x > 1) return 1;
  return x;
}

int readAdcAvg(int pin) {
  long sum = 0;
  for (int i = 0; i < 8; i++) {
    sum += analogRead(pin);
    delayMicroseconds(50);
  }
  return sum / 8;
}

// -------------------- Setup --------------------
void setup() {
  Serial.begin(115200);
  delay(1000);

  Serial.println("AURAID STARTING...");

  Wire.begin(PIN_SDA, PIN_SCL);

  // BNO
  if (bno.begin()) {
    bno_ok = true;
    Serial.println("BNO055 OK");
    bno.setExtCrystalUse(true);
    bno.setMode(OPERATION_MODE_NDOF);
  } else {
    Serial.println("BNO055 NOT DETECTED");
  }

  // WIFI CONNECT
  Serial.println("Connecting to WiFi...");

  WiFi.begin(ssid, password);

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  Serial.println("\n WiFi Connected!");
  Serial.print("ESP32 IP: ");
  Serial.println(WiFi.localIP());

  Serial.print("Target Host: ");
  Serial.println(host);

  Serial.println("AURAID READY");
}

// -------------------- Compression --------------------
void updateCompression(float fMax) {
  if (!g_inCompression && fMax >= CMP_ON) {
    g_inCompression = true;
    g_cmpTotal++;
  } else if (g_inCompression && fMax <= CMP_OFF) {
    g_inCompression = false;
  }
}

// -------------------- Loop --------------------
void loop() {

  uint32_t now = millis();

  if (now - g_lastSampleMs >= SAMPLE_INTERVAL_MS) {
    g_lastSampleMs = now;

    float fn[3];

    for (int i = 0; i < 3; i++) {
      int raw = readAdcAvg(PIN_FSR[i]);
      fn[i] = raw / 4095.0;
    }

    float fMax = max(fn[0], max(fn[1], fn[2]));
    updateCompression(fMax);

    float motion01 = 0;
    float tilt = 0;

    if (bno_ok) {
      imu::Vector<3> lin = bno.getVector(Adafruit_BNO055::VECTOR_LINEARACCEL);
      float mag = sqrt(lin.x()*lin.x() + lin.y()*lin.y() + lin.z()*lin.z());
      motion01 = clamp01(mag / MOTION_ACCEL_MAX);

      imu::Vector<3> euler = bno.getVector(Adafruit_BNO055::VECTOR_EULER);
      float eulerV[3] = {euler.x(), euler.y(), euler.z()};
      tilt = abs(eulerV[TILT_EULER_INDEX]);
      if (tilt > TILT_MAX_DEG) tilt = TILT_MAX_DEG;
    }

    if (now - g_lastPrintMs >= PRINT_INTERVAL_MS) {
      g_lastPrintMs = now;

      // SERIAL OUTPUT
      Serial.printf("AURAID,%lu,%.3f,%.3f,%.3f,%.3f,%.3f,%.1f,%lu\n",
        now, fn[0], fn[1], fn[2], fMax,
        motion01, tilt, g_cmpTotal);

      // TCP CONNECT
      if (!client.connected()) {
        if (client.connect(host, port)) {
          Serial.println("TCP connected to Unity");
        } else if (now - g_lastTcpFailLogMs >= 3000) {
          g_lastTcpFailLogMs = now;
          Serial.println("TCP connect failed");
        }
      }

      // SEND DATA
      if (client.connected()) {
        char data[128];
        snprintf(data, sizeof(data),
          "%.3f,%.3f,%.3f,%.3f,%.3f,%.1f,%lu",
          fn[0], fn[1], fn[2], fMax,
          motion01, tilt, g_cmpTotal);

        client.println(data);
      }
    }
  }
}