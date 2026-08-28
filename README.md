# STAJ22001 - Yazılım Mühendisliği Staj Projeleri

Bu depo, 2026 yılı yaz dönemi donanım-yazılım entegrasyonu stajı kapsamında geliştirilen iki farklı mühendislik projesinin kaynak kodlarını içermektedir. Projeler, mikrodenetleyici tabanlı sensör okuma ve endüstriyel CNC makine kontrolü alanlarında geliştirilmiştir.

## 📂 Depo İçeriği ve Proje Yapısı

Repo içerisinde iki ana klasör bulunmaktadır:

1. **`AS7341_Diagnostic_System/`**: Tıbbi teşhis amaçlı spektral renk analiz projesi (C/C++).
2. **`CNC_GCode_Sender/`**: Endüstriyel CNC tezgâhları için hareket kontrol arayüzü (C# / Windows Forms).

---

## 🛠️ Proje 1: Balgam Örneği Teşhis Sistemi (AS7341 & XMC1100)

Bu proje, tüberküloz ve diğer mikobakterilerin üreme durumunu spektral analiz yöntemiyle tespit etmek amacıyla geliştirilmiştir. Infineon XMC1100 mikrodenetleyicisi ve AS7341 11-kanallı spektral sensör kullanılarak balgam numunelerinin renk kırılımları incelenmektedir.

### Öne Çıkan Özellikler
* **I2C Haberleşme & SMUX Konfigürasyonu:** Sensör üzerindeki farklı dalga boylarının (F1-F8) eş zamanlı ve optimize edilmiş şekilde okunması.
* **Otomatik Kalibrasyon:** Ölçüm öncesi ortam ışığının ve referans tüp değerlerinin hesaplanarak ham verinin (raw data) normalize edilmesi.
* **Karar Algoritması:** Gelen spektral verilere göre (Mor kümesi ve F5-F8 yoğunlukları) tıbbi durum teşhisi (Steril, Kontaminasyon, Pozitif Üreme vb.) yapılması.
* **Donanımsal Geri Bildirim:** Elde edilen tıbbi analiz sonucuna göre Kırmızı ve Yeşil LED'ler aracılığıyla senkronize görsel bildirim sağlanması.

### Kullanılan Teknolojiler
* **Donanım:** Infineon XMC1100 Geliştirme Kartı, AS7341 Spektral Renk Sensörü
* **Yazılım:** C/C++, Arduino IDE, Wire.h Kütüphanesi

---

## ⚙️ Proje 2: CNC G-Code Ayrıştırıcı ve Hareket Kontrol Arayüzü

Bu proje, Baldor NextMove ESB CNC hareket kontrol kartı ile bilgisayar arasındaki iletişimi sağlamak için geliştirilmiş bir masaüstü arayüzüdür. Kullanıcıların `.nc` veya `.txt` uzantılı G-Code dosyalarını sisteme yüklemesine ve fiziksel eksen hareketlerini yönetmesine olanak tanır.

### Öne Çıkan Özellikler
* **G-Code Parsing Algoritması:** Yüklenen dosyalardaki hareket komutlarını ve eksen koordinatlarını (X, Y, Z) satır satır ayrıştıran Regex tabanlı okuma sistemi.
* **MintControls Entegrasyonu:** Ayrıştırılan verilerin COM kütüphaneleri üzerinden Baldor hareket kontrolörünün anlayacağı formatlara (MoveA, MoveR) dönüştürülüp donanıma iletilmesi.
* **Endüstriyel Güvenlik:** İletişim kopuklukları veya hatalı G-Code satırları için `try-catch` tabanlı Exception Handling ve Acil Durdurma (Emergency Stop) entegrasyonu.
* **Kullanıcı Arayüzü (UI):** Eksenlerin manuel olarak sürülebileceği (Jogging) ve cihaz loglarının anlık izlenebileceği Windows Forms tasarımı.

### Kullanılan Teknolojiler
* **Donanım:** Baldor NextMove ESB CNC Kontrolcüsü
* **Yazılım:** C#, .NET Framework, Windows Forms, Visual Studio, MintControls Kütüphanesi

---

## 🚀 Kurulum ve Çalıştırma

### Teşhis Sistemi (Sensör) İçin:
1. `AS7341_Diagnostic_System` klasöründeki `.ino` dosyasını Arduino IDE ile açın.
2. Kart yöneticisinden Infineon XMC kart ailesini seçin ve XMC1100'ü ayarlayın.
3. Donanım bağlantılarını (I2C pinleri ve LED'ler) kod içerisinde belirtildiği şekilde yapın ve kodu derleyip yükleyin.

### CNC G-Code Sender İçin:
1. Visual Studio kullanarak `CNC_GCode_Sender/` dizinindeki `.sln` (Solution) dosyasını açın.
2. Projenin bağımlılıkları arasında yer alan `Interop.MintControls_5864Lib.dll` referansının doğru eklendiğinden emin olun.
3. Projeyi *Release* veya *Debug* modunda derleyerek başlatın. (Fiziksel testler için USB/Seri port üzerinden Baldor cihazının bağlı olması gerekmektedir.)

---
*Bu depo 2026 yılı zorunlu staj uygulaması dahilinde oluşturulmuştur.*
