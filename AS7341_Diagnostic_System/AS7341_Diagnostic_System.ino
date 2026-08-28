// ============================================================
//  AS7341 Besiyeri Renk Teşhis Sistemi - Arduino IDE (XMC1100)
// ============================================================

#include <Wire.h>
#include <math.h>

#define AS7341_ADDR       0x39

#define REG_ENABLE        0x80
#define REG_ATIME         0x81
#define REG_CFG6          0xAF
#define REG_ASTEP_L       0xCA
#define REG_ASTEP_H       0xCB
#define REG_CH0_DATA_L    0x95
#define REG_STATUS2       0xA3
#define AVALID_BIT        0x40
#define REG_CONFIG        0x70  // LED_SEL
#define REG_LED           0x74  // LED_ACT ve akim seviyesi
#define REG_CFG0          0xA9  // REG_BANK (0x60-0x74 erisimi icin)

#define LED_PIN_RED    2
#define LED_PIN_GREEN  3

// --- OLCUM ARALIGI (ms)  ---
#define LOOP_DELAY_MS  2000

// --- KALIBRASYON AYARLARI ---
#define CAL_SAMPLE_COUNT  10   // referans olcumu icin ortalama alinacak ornek sayisi

typedef enum {
    TK_STATUS_UNKNOWN = 0,
    TK_STATUS_PURPLE,
    TK_STATUS_GREEN,
    TK_STATUS_YELLOW,
    TK_STATUS_ORANGE,
    TK_STATUS_RED
} TK_Medium_Status_t;

TK_Medium_Status_t current_tube_status = TK_STATUS_UNKNOWN;
uint32_t loop_counter = 0;

const char* channel_names[8] = {"F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8"};
float baseline[8] = {1, 1, 1, 1, 1, 1, 1, 1}; // kalibrasyondan sonra doldurulacak

// --- I2C YAZMA/OKUMA ---
void AS7341_Write_Reg(uint8_t reg, uint8_t value) {
    Wire.beginTransmission(AS7341_ADDR);
    Wire.write(reg);
    Wire.write(value);
    Wire.endTransmission();
}

uint8_t AS7341_Read_Reg(uint8_t reg) {
    Wire.beginTransmission(AS7341_ADDR);
    Wire.write(reg);
    Wire.endTransmission(false);
    Wire.requestFrom(AS7341_ADDR, (uint8_t)1);
    if (Wire.available()) {
        return Wire.read();
    }
    return 0;
}

void AS7341_Write_Buffer(uint8_t reg, const uint8_t *data, uint8_t length) {
    Wire.beginTransmission(AS7341_ADDR);
    Wire.write(reg);
    for (uint8_t i = 0; i < length; i++) {
        Wire.write(data[i]);
    }
    Wire.endTransmission();
}

void AS7341_Set_SMUX(bool is_F1_F4) {
    static const uint8_t smux_f1_f4[20] = {0x30, 0x01, 0x00, 0x00, 0x00, 0x42, 0x00, 0x00, 0x50, 0x00, 0x00, 0x00, 0x20, 0x04, 0x00, 0x30, 0x01, 0x50, 0x00, 0x06};
    static const uint8_t smux_f5_f8[20] = {0x00, 0x00, 0x00, 0x40, 0x02, 0x00, 0x10, 0x03, 0x50, 0x10, 0x03, 0x00, 0x00, 0x00, 0x24, 0x00, 0x00, 0x50, 0x00, 0x06};

    AS7341_Write_Reg(REG_ENABLE, 0x01);
    AS7341_Write_Reg(REG_CFG6, 0x10);

    if (is_F1_F4) {
        AS7341_Write_Buffer(0x00, smux_f1_f4, 20);
    } else {
        AS7341_Write_Buffer(0x00, smux_f5_f8, 20);
    }

    AS7341_Write_Reg(REG_ENABLE, 0x11);
    delay(5);
    AS7341_Write_Reg(REG_ENABLE, 0x03);
}

bool AS7341_Wait_For_Data(uint16_t timeout_ms) {
    uint32_t start = millis();
    while ((millis() - start) < timeout_ms) {
        uint8_t status = AS7341_Read_Reg(REG_STATUS2);
        if (status & AVALID_BIT) return true;
        delay(2);
    }
    return false;
}

bool AS7341_Read_Target_Channels(uint16_t *channels) {
    uint8_t raw[8];

    // 1. FAZ: F1,F2,F3,F4 - donanim zaten 4'unu birden olcuyor
    AS7341_Set_SMUX(true);
    if (!AS7341_Wait_For_Data(300)) return false;

    Wire.beginTransmission(AS7341_ADDR);
    Wire.write(REG_CH0_DATA_L);
    Wire.endTransmission(false);
    Wire.requestFrom(AS7341_ADDR, (uint8_t)8);
    for (uint8_t i = 0; i < 8; i++) {
        raw[i] = Wire.available() ? Wire.read() : 0;
    }
    channels[0] = (uint16_t)(raw[1] << 8) | raw[0]; // F1
    channels[1] = (uint16_t)(raw[3] << 8) | raw[2]; // F2
    channels[2] = (uint16_t)(raw[5] << 8) | raw[4]; // F3
    channels[3] = (uint16_t)(raw[7] << 8) | raw[6]; // F4

    // 2. FAZ: F5, F6, F7, F8
    AS7341_Set_SMUX(false);
    if (!AS7341_Wait_For_Data(300)) return false;

    Wire.beginTransmission(AS7341_ADDR);
    Wire.write(REG_CH0_DATA_L);
    Wire.endTransmission(false);
    Wire.requestFrom(AS7341_ADDR, (uint8_t)8);
    for (uint8_t i = 0; i < 8; i++) {
        raw[i] = Wire.available() ? Wire.read() : 0;
    }
    channels[4] = (uint16_t)(raw[1] << 8) | raw[0]; // F5
    channels[5] = (uint16_t)(raw[3] << 8) | raw[2]; // F6
    channels[6] = (uint16_t)(raw[5] << 8) | raw[4]; // F7
    channels[7] = (uint16_t)(raw[7] << 8) | raw[6]; // F8

    return true;
}

// --- REGISTER BANKASI DEGISTIRME (0x60-0x74 araligina erismek icin) ---
void AS7341_Set_Low_Bank(bool low_bank) {
    uint8_t cfg0 = AS7341_Read_Reg(REG_CFG0);
    if (low_bank) {
        cfg0 |= 0x10;  // REG_BANK=1 -> 0x60-0x74 registerlarina erisim acilir
    } else {
        cfg0 &= ~0x10; // REG_BANK=0 -> normal olcum registerlarina (0x80+) don
    }
    AS7341_Write_Reg(REG_CFG0, cfg0);
}

// --- SENSOR UZERINDEKI AYDINLATMA LED'INI AC/KAPAT ---
// current_step: 0-127 arasi, gercek akim(mA) = current_step*2 + 4
void AS7341_Set_LED(bool enable, uint8_t current_step) {
    AS7341_Set_Low_Bank(true); // 0x60-0x74 bankasina gec

    uint8_t config = AS7341_Read_Reg(REG_CONFIG);
    config |= 0x08; // LED_SEL biti (bit3) - LED kontrolunu register uzerinden yap
    AS7341_Write_Reg(REG_CONFIG, config);

    uint8_t led_value = (enable ? 0x80 : 0x00) | (current_step & 0x7F);
    AS7341_Write_Reg(REG_LED, led_value);

    AS7341_Set_Low_Bank(false); // normal bankaya geri don
}

bool AS7341_Init(void) {
    AS7341_Write_Reg(REG_ENABLE, 0x01);
    AS7341_Write_Reg(REG_ATIME, 29);
    AS7341_Write_Reg(REG_ASTEP_L, 0xE7);
    AS7341_Write_Reg(REG_ASTEP_H, 0x03);

    // Led'in parlaklik ayari (0-127) arasi degistirilebilir
    AS7341_Set_LED(true, 10);

    uint8_t check = AS7341_Read_Reg(REG_ENABLE);
    return (check != 0xFF);
}

// --- BASLANGICTA REFERANS KALIBRASYONU ---
// Bu fonksiyon calisirken sensore beyaz/notr bir referans (bos tup, beyaz kagit vs.) tutuluyor.
void AS7341_Calibrate_Baseline() {
    Serial.println("KALIBRASYON: Sensore beyaz/notr referansi tut, bekleniyor...");
    delay(2000); // referansi yerlestirme suresi

    uint32_t sum[8] = {0, 0, 0, 0, 0, 0, 0, 0};
    uint8_t valid_samples = 0;

    for (uint8_t i = 0; i < CAL_SAMPLE_COUNT; i++) {
        uint16_t ch[8] = {0};
        if (AS7341_Read_Target_Channels(ch)) {
            for (uint8_t j = 0; j < 8; j++) sum[j] += ch[j];
            valid_samples++;
        }
        delay(100);
    }

    if (valid_samples > 0) {
        for (uint8_t j = 0; j < 8; j++) {
            baseline[j] = (float)sum[j] / valid_samples;
            if (baseline[j] < 1) baseline[j] = 1; // sifira bolme koruma
        }
    }

    Serial.print("KALIBRASYON TAMAM -> ");
    for (uint8_t j = 0; j < 8; j++) {
        Serial.print(channel_names[j]);
        Serial.print(":");
        Serial.print(baseline[j]);
        Serial.print(" ");
    }
    Serial.println();
}

#define MIN_RAW_THRESHOLD   50    // bu esigin altindaki kanal hesaba katmiyoruz (gurultu filtresi)

TK_Medium_Status_t AS7341_Analyze_Medium(uint16_t *f_data) {
    // f_data[0..7] = F1,F2,F3,F4,F5,F6,F7,F8
    uint32_t total = 0;
    for (uint8_t i = 0; i < 8; i++) total += f_data[i];

    Serial.println("--- Ham degerler ---");
    for (uint8_t i = 0; i < 8; i++) {
        Serial.print(channel_names[i]);
        Serial.print(" [");
        Serial.print(f_data[i]);
        Serial.print("]\t");
        int stars = f_data[i] / 50;
        for (int s = 0; s < stars && s < 40; s++) Serial.print('*');
        Serial.println();
    }

    if (total < 200) {
        return TK_STATUS_UNKNOWN;
    }

    // 1. ADIM: Kalibrasyona gore normalize et
    float calibrated[8];
    for (uint8_t i = 0; i < 8; i++) {
        calibrated[i] = (f_data[i] < MIN_RAW_THRESHOLD) ? 0 : (float)f_data[i] / baseline[i];
    }

    // 2. ADIM: "Mor kumesi" - F1+F2+F3+F4 (415-515nm, mor/mavi/camgobegi)
    // toplu olarak diger kanallara gore ne kadar guclu?
    float purple_cluster = (calibrated[0] + calibrated[1] + calibrated[2] + calibrated[3]) / 4.0f;

    Serial.print("[DEBUG] Mor kumesi ortalamasi (F1-F4): ");
    Serial.println(purple_cluster);

    // 3. ADIM: F5,F6,F7,F8 arasinda hangisi en yuksek?
    uint8_t max_idx = 4; // F5'ten basla (dizide index 4)
    float max_value = calibrated[4];
    for (uint8_t i = 5; i < 8; i++) {
        if (calibrated[i] > max_value) {
            max_value = calibrated[i];
            max_idx = i;
        }
    }

    Serial.print("[DEBUG] F5-F8 icinde en yuksek: ");
    Serial.print(channel_names[max_idx]);
    Serial.print(" (");
    Serial.print(max_value);
    Serial.println(")");

    // 4. ADIM: Mor kumesi, F5-F8'in en yukseginden daha baskinsa -> MOR
    if (purple_cluster > max_value) {
        return TK_STATUS_PURPLE;
    }

    switch (max_idx) {
        case 4: return TK_STATUS_GREEN;   // F5
        case 5: return TK_STATUS_YELLOW;  // F6
        case 6: return TK_STATUS_ORANGE;  // F7
        case 7: return TK_STATUS_RED;     // F8
        default: return TK_STATUS_UNKNOWN;
    }
}

void Send_Diagnosis_Message(TK_Medium_Status_t status) {
    switch (status) {
        case TK_STATUS_PURPLE:
            Serial.println("Mor: Yuksek pH / Islem Hatasi (Nortalisazyon Bozuk)");
            break;
        case TK_STATUS_GREEN:
            Serial.println("Yesil: Kontaminasyon (Bulasma / Kirlenme)");
            break;
        case TK_STATUS_YELLOW:
            Serial.println("Sari: Pozitif Ureme (Mikobakteri)");
            break;
        case TK_STATUS_ORANGE:
            Serial.println("Turuncu; Pozitif Ureme ihtimali var (Mikrobakteri)");
            break;
        case TK_STATUS_RED:
            Serial.println("Kirmizi: Steril / Ureme Yok");
            break;
        default:
            Serial.println("DURUM: BILINMIYOR / OLCUM BEKLENIYOR");
            break;
    }
}

void Control_LED_Signals(TK_Medium_Status_t status) {
    loop_counter++;
    switch (status) {
        case TK_STATUS_RED:
            digitalWrite(LED_PIN_RED, HIGH);
            digitalWrite(LED_PIN_GREEN, LOW);
            break;
        case TK_STATUS_YELLOW:
        case TK_STATUS_ORANGE:
            digitalWrite(LED_PIN_RED, LOW);
            digitalWrite(LED_PIN_GREEN, HIGH);
            break;
        case TK_STATUS_GREEN:
            digitalWrite(LED_PIN_RED, (loop_counter % 2) == 0);
            digitalWrite(LED_PIN_GREEN, (loop_counter % 2) != 0);
            break;
        case TK_STATUS_PURPLE:
            if ((loop_counter % 4) < 2) {
                digitalWrite(LED_PIN_RED, HIGH);
                digitalWrite(LED_PIN_GREEN, HIGH);
            } else {
                digitalWrite(LED_PIN_RED, LOW);
                digitalWrite(LED_PIN_GREEN, LOW);
            }
            break;
        default:
            digitalWrite(LED_PIN_RED, LOW);
            digitalWrite(LED_PIN_GREEN, LOW);
            break;
    }
}

bool sensor_ready = false;

void setup() {
    Serial.begin(9600);
    while (!Serial) { delay(1); }

    pinMode(LED_PIN_RED, OUTPUT);
    pinMode(LED_PIN_GREEN, OUTPUT);

    Wire.begin();

    Serial.println("AS7341 baslatiliyor...");
    sensor_ready = AS7341_Init();

    if (!sensor_ready) {
        Serial.println("HATA: AS7341 bulunamadi! Baglantiyi kontrol et.");
    } else {
        Serial.println("AS7341 hazir.");
        AS7341_Calibrate_Baseline();
        Serial.println("Olcum basliyor...");
    }
}

void loop() {
    uint16_t target_channels[8] = {0};

    if (sensor_ready) {
        if (AS7341_Read_Target_Channels(target_channels)) {
            current_tube_status = AS7341_Analyze_Medium(target_channels);
        } else {
            Serial.println("HATA: Veri zaman asimina ugradi!");
            current_tube_status = TK_STATUS_UNKNOWN;
        }
    } else {
        current_tube_status = TK_STATUS_UNKNOWN;
    }

    Control_LED_Signals(current_tube_status);
    Send_Diagnosis_Message(current_tube_status);

    delay(LOOP_DELAY_MS);
}