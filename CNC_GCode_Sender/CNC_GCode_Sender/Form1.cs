using MintControls_5864Lib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CNC_GCode_Sender
{
    public partial class Form1 : Form
    {
        MintControls_5864Lib.MintController mintController;

        // ===== Bağlantı ayarları kontrolleri =====
        private ComboBox cmbComPort;
        private ComboBox cmbBaudRate;
        private Button btnRefreshPorts;
        private Panel pnlStatusLight;

        // ===== Gönderim kontrolleri =====
        private Button btnGonder;
        private Button btnDuraklat;
        private Button btnAcilDurdur;
        private ProgressBar progressBar1;
        private Label lblIlerleme;

        // ===== Detay paneli =====
        private Panel pnlDetay;
        private Label lblDetayX, lblDetayY, lblDetayZ, lblDetayF;

        // ===== Gönderim durumu =====
        private List<GCodeCommand> parsedCommands = new List<GCodeCommand>();
        private CancellationTokenSource sendCts;
        private volatile bool isPaused = false;
        private bool isConnected = false;

        // ===== Makine sınırları (KENDİ MAKİNENE GÖRE DÜZENLE) =====
        private const float X_MIN = 0f, X_MAX = 300f;
        private const float Y_MIN = 0f, Y_MAX = 200f;
        private const float Z_MIN = -50f, Z_MAX = 50f;

        // Ayarları saklayacağımız dosya (proje ayarları eklemeye gerek kalmasın diye basit text dosyası)
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BaldorCNC_settings.txt");

        public Form1()
        {
            InitializeComponent();

            // ===== ANA EKRAN =====
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(780, 560);
            this.Text = "Baldor CNC Kontrol Merkezi";

            // ===== ÜST BAŞLIK ÇUBUĞU =====
            Panel pnlHeader = new Panel();
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 70;
            pnlHeader.BackColor = Color.FromArgb(37, 37, 38);

            Label lblTitle = new Label();
            lblTitle.Text = "⚙ BALDOR CNC KONTROL MERKEZİ";
            lblTitle.ForeColor = Color.Gainsboro;
            lblTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(15, 22);
            pnlHeader.Controls.Add(lblTitle);

            // Durum ışığı (bağlan butonunun soluna)
            pnlStatusLight = new Panel();
            pnlStatusLight.Size = new Size(18, 18);
            pnlStatusLight.Location = new Point(pnlHeader.Width - 345, 26);
            pnlStatusLight.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pnlStatusLight.Paint += (s, e) => DrawStatusCircle(e.Graphics, isConnected);
            pnlHeader.Controls.Add(pnlStatusLight);

            button1.Parent = pnlHeader;
            button2.Parent = pnlHeader;
            button1.Size = new Size(150, 40);
            button2.Size = new Size(150, 40);
            button1.Location = new Point(pnlHeader.Width - 320, 15);
            button2.Location = new Point(pnlHeader.Width - 160, 15);
            button1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button2.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            foreach (var btn in new[] { button1, button2 })
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.BackColor = Color.FromArgb(255, 165, 0);
                btn.ForeColor = Color.Black;
                btn.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                btn.Cursor = Cursors.Hand;
                btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(255, 140, 0);
                btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(255, 165, 0);
            }

            // ===== BAĞLANTI AYARLARI ŞERİDİ =====
            Panel pnlConnection = new Panel();
            pnlConnection.Dock = DockStyle.Top;
            pnlConnection.Height = 46;
            pnlConnection.BackColor = Color.FromArgb(33, 33, 34);

            Label lblPort = new Label { Text = "COM Port:", ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 9F), AutoSize = true, Location = new Point(15, 13) };

            cmbComPort = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(95, 10), Width = 90, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.White };

            btnRefreshPorts = new Button { Text = "⟳", Location = new Point(190, 9), Size = new Size(30, 26), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White, Cursor = Cursors.Hand };
            btnRefreshPorts.FlatAppearance.BorderSize = 0;
            btnRefreshPorts.Click += (s, e) => PopulateComPorts();

            Label lblBaud = new Label { Text = "Baud:", ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 9F), AutoSize = true, Location = new Point(235, 13) };

            cmbBaudRate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(280, 10), Width = 90, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(45, 45, 48), ForeColor = Color.White };
            cmbBaudRate.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });

            pnlConnection.Controls.Add(lblPort);
            pnlConnection.Controls.Add(cmbComPort);
            pnlConnection.Controls.Add(btnRefreshPorts);
            pnlConnection.Controls.Add(lblBaud);
            pnlConnection.Controls.Add(cmbBaudRate);

            // ===== GÖNDERİM ŞERİDİ (Gönder / Duraklat / Acil Dur / Progress) =====
            Panel pnlSend = new Panel();
            pnlSend.Dock = DockStyle.Top;
            pnlSend.Height = 56;
            pnlSend.BackColor = Color.FromArgb(33, 33, 34);

            btnGonder = new Button { Text = "▶ Gönder", Location = new Point(15, 10), Size = new Size(110, 36) };
            btnDuraklat = new Button { Text = "⏸ Duraklat", Location = new Point(135, 10), Size = new Size(110, 36), Enabled = false };
            btnAcilDurdur = new Button { Text = "⛔ ACİL DUR", Location = new Point(255, 10), Size = new Size(130, 36) };

            foreach (var btn in new[] { btnGonder, btnDuraklat })
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
                btn.BackColor = Color.FromArgb(255, 165, 0);
                btn.ForeColor = Color.Black;
                btn.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                btn.Cursor = Cursors.Hand;
            }
            btnAcilDurdur.FlatStyle = FlatStyle.Flat;
            btnAcilDurdur.FlatAppearance.BorderSize = 0;
            btnAcilDurdur.BackColor = Color.Firebrick;
            btnAcilDurdur.ForeColor = Color.White;
            btnAcilDurdur.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAcilDurdur.Cursor = Cursors.Hand;

            btnGonder.Click += BtnGonder_Click;
            btnDuraklat.Click += BtnDuraklat_Click;
            btnAcilDurdur.Click += BtnAcilDurdur_Click;

            progressBar1 = new ProgressBar { Location = new Point(400, 14), Size = new Size(280, 22) };
            lblIlerleme = new Label { Text = "Hazır", ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 8F), AutoSize = true, Location = new Point(400, 38) };

            pnlSend.Controls.Add(btnGonder);
            pnlSend.Controls.Add(btnDuraklat);
            pnlSend.Controls.Add(btnAcilDurdur);
            pnlSend.Controls.Add(progressBar1);
            pnlSend.Controls.Add(lblIlerleme);

            // ===== DETAY PANELİ (sağ taraf, seçili satırın X/Y/Z/F büyük gösterimi) =====
            pnlDetay = new Panel();
            pnlDetay.Dock = DockStyle.Right;
            pnlDetay.Width = 200;
            pnlDetay.BackColor = Color.FromArgb(37, 37, 38);

            Label lblDetayBaslik = new Label { Text = "SATIR DETAYI", ForeColor = Color.DarkGray, Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(15, 15) };
            lblDetayX = new Label { Text = "X: -", ForeColor = Color.White, Font = new Font("Consolas", 14F), AutoSize = true, Location = new Point(15, 50) };
            lblDetayY = new Label { Text = "Y: -", ForeColor = Color.White, Font = new Font("Consolas", 14F), AutoSize = true, Location = new Point(15, 85) };
            lblDetayZ = new Label { Text = "Z: -", ForeColor = Color.White, Font = new Font("Consolas", 14F), AutoSize = true, Location = new Point(15, 120) };
            lblDetayF = new Label { Text = "F: -", ForeColor = Color.White, Font = new Font("Consolas", 14F), AutoSize = true, Location = new Point(15, 155) };

            pnlDetay.Controls.Add(lblDetayBaslik);
            pnlDetay.Controls.Add(lblDetayX);
            pnlDetay.Controls.Add(lblDetayY);
            pnlDetay.Controls.Add(lblDetayZ);
            pnlDetay.Controls.Add(lblDetayF);

            // ===== G-CODE LİSTESİ =====
            listBox1.Parent = this;
            listBox1.Dock = DockStyle.Fill;
            listBox1.BackColor = Color.FromArgb(45, 45, 48);
            listBox1.ForeColor = Color.LimeGreen;
            listBox1.Font = new Font("Consolas", 10F);
            listBox1.BorderStyle = BorderStyle.FixedSingle;
            listBox1.SelectedIndexChanged += ListBox1_SelectedIndexChanged;

            // Sıralama: Fill olan en son eklenmeli, Right/Top dock'lar ondan önce
            this.Controls.Add(listBox1);
            this.Controls.Add(pnlDetay);
            this.Controls.Add(pnlSend);
            this.Controls.Add(pnlConnection);
            this.Controls.Add(pnlHeader);

            PopulateComPorts();
            LoadSettings();
        }

        // ================== DURUM IŞIĞI ==================
        private void DrawStatusCircle(Graphics g, bool connected)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Brush b = new SolidBrush(connected ? Color.LimeGreen : Color.Firebrick))
            {
                g.FillEllipse(b, 0, 0, 17, 17);
            }
        }

        private void SetConnected(bool connected)
        {
            isConnected = connected;
            pnlStatusLight.Invalidate(); // yeniden çizdir
        }

        // ================== COM PORT TARAMA ==================
        private void PopulateComPorts()
        {
            string previouslySelected = cmbComPort.SelectedItem?.ToString();
            cmbComPort.Items.Clear();
            string[] ports = SerialPort.GetPortNames();

            if (ports.Length == 0)
            {
                cmbComPort.Items.Add("Port bulunamadı");
                cmbComPort.SelectedIndex = 0;
                return;
            }

            cmbComPort.Items.AddRange(ports);

            if (previouslySelected != null && cmbComPort.Items.Contains(previouslySelected))
                cmbComPort.SelectedItem = previouslySelected;
            else
                cmbComPort.SelectedIndex = 0;
        }

        // ================== AYARLARI KAYDET / YÜKLE ==================
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) { cmbBaudRate.SelectedItem = "115200"; return; }

                string[] lines = File.ReadAllLines(SettingsPath);
                string savedPort = lines.Length > 0 ? lines[0] : null;
                string savedBaud = lines.Length > 1 ? lines[1] : "115200";

                if (savedPort != null && cmbComPort.Items.Contains(savedPort))
                    cmbComPort.SelectedItem = savedPort;

                if (cmbBaudRate.Items.Contains(savedBaud))
                    cmbBaudRate.SelectedItem = savedBaud;
                else
                    cmbBaudRate.SelectedItem = "115200";
            }
            catch { cmbBaudRate.SelectedItem = "115200"; }
        }

        private void SaveSettings()
        {
            try
            {
                File.WriteAllLines(SettingsPath, new[]
                {
                    cmbComPort.SelectedItem?.ToString() ?? "",
                    cmbBaudRate.SelectedItem?.ToString() ?? "115200"
                });
            }
            catch { /* ayar kaydedilemezse sessizce geç, kritik değil */ }
        }

        // ================== DOSYA SEÇME ==================
        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "G-Code Dosyaları|*.gcode;*.nc;*.txt|Tüm Dosyalar|*.*";
            openFileDialog.Title = "Bir G-Code Dosyası Seçin";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                string[] gcodeLines = File.ReadAllLines(filePath);

                listBox1.Items.Clear();
                parsedCommands.Clear();

                foreach (string line in gcodeLines)
                {
                    GCodeCommand cmd = new GCodeCommand(line);

                    if (!string.IsNullOrWhiteSpace(cmd.RawLine) && !cmd.RawLine.StartsWith(";"))
                    {
                        string displayString = $"Komut: {cmd.CommandType,-4} | X: {cmd.X,-5} | Y: {cmd.Y,-5} | Z: {cmd.Z,-4} | F: {cmd.F}";
                        listBox1.Items.Add(displayString);
                        parsedCommands.Add(cmd);
                    }
                }

                this.Text = $"Baldor CNC Kontrol Merkezi — {Path.GetFileName(filePath)}";
                progressBar1.Maximum = Math.Max(parsedCommands.Count, 1);
                progressBar1.Value = 0;
                lblIlerleme.Text = $"{parsedCommands.Count} komut yüklendi";
            }
        }

        // ================== SATIR SEÇİLİNCE DETAY GÖSTER ==================
        private void ListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int i = listBox1.SelectedIndex;
            if (i < 0 || i >= parsedCommands.Count) return;

            var cmd = parsedCommands[i];
            lblDetayX.Text = $"X: {cmd.X}";
            lblDetayY.Text = $"Y: {cmd.Y}";
            lblDetayZ.Text = $"Z: {cmd.Z}";
            lblDetayF.Text = $"F: {cmd.F}";
        }

        // ================== BAĞLAN ==================
        private void BtnBaglan_Click(object sender, EventArgs e)
        {
            if (cmbComPort.SelectedItem == null || cmbComPort.SelectedItem.ToString() == "Port bulunamadı")
            {
                MessageBox.Show("Geçerli bir COM port seçmelisin. Yenile butonuna basıp tekrar dene.", "Uyarı");
                return;
            }

            try
            {
                string selectedPort = cmbComPort.SelectedItem.ToString();
                short portNumber = short.Parse(new string(selectedPort.Where(char.IsDigit).ToArray()));
                int baudRate = int.Parse(cmbBaudRate.SelectedItem.ToString());

                mintController = new MintControllerClass();
                mintController.SetNextMoveESBLink((short)0, portNumber, baudRate, true);
                //mintController.DriveEnable = true;   // <-- eğer ki sürücüler etkin olmazsa bunu ekle
                /*mintController.DoReset((short)0);
                mintController.DoReset((short)1);
                mintController.DoReset((short)2);*/ //eğer ki eksen hata durumundaysa bunu ekle.

                SetConnected(true);
                SaveSettings();
                MessageBox.Show($"Makine ile bağlantı başarılı! (Port: {selectedPort}, Baud: {baudRate})", "Bağlantı Durumu");
            }
            catch (Exception ex)
            {
                SetConnected(false);
                MessageBox.Show("Bağlantı hatası: " + ex.Message, "Hata");
            }
        }

        // ================== SINIR (LIMIT) KONTROLÜ ==================
        // NOT: GCodeCommand.X/Y/Z alanlarının gerçek tipini (float? / string) görmedim,
        // bu yüzden burada float.TryParse ile genel/esnek bir kontrol yaptım.
        // Kendi GCodeCommand.cs'ine göre bu kısmı uyarlaman gerekebilir.
        private bool ValidateBounds(out string hataMesaji)
        {
            hataMesaji = null;
            for (int i = 0; i < parsedCommands.Count; i++)
            {
                var cmd = parsedCommands[i];

                if (float.TryParse(cmd.X?.ToString(), out float x) && (x < X_MIN || x > X_MAX))
                { hataMesaji = $"Satır {i + 1}: X={x} çalışma alanı dışında ({X_MIN}-{X_MAX})"; return false; }

                if (float.TryParse(cmd.Y?.ToString(), out float y) && (y < Y_MIN || y > Y_MAX))
                { hataMesaji = $"Satır {i + 1}: Y={y} çalışma alanı dışında ({Y_MIN}-{Y_MAX})"; return false; }

                if (float.TryParse(cmd.Z?.ToString(), out float z) && (z < Z_MIN || z > Z_MAX))
                { hataMesaji = $"Satır {i + 1}: Z={z} çalışma alanı dışında ({Z_MIN}-{Z_MAX})"; return false; }
            }
            return true;
        }

        // ================== TEK SATIRI MAKİNEYE GÖNDER ==================
        // Eksen eşleşmesi: 0 = X, 1 = Y, 2 = Z (kartın X1/X2/X3 konnektörlerine göre;
        // kendi kablolamanla uyuşmuyorsa bu üç sayıyı değiştirmen yeterli).
        //
        // 2 veya daha fazla eksen aynı anda değişiyorsa (örn. G1 X20 Y0) -> VectorA + DoGo1
        // (koordineli/çapraz hareket, VectorA en az 2 eksen ister)
        // Sadece 1 eksen değişiyorsa (örn. sadece Z inişi) -> set_MoveA + DoGo1
        //
        // NOT: cmd.X / cmd.Y / cmd.Z / cmd.F alanları GCodeCommand.cs'te double? —
        // Mint API'si float istediği için aşağıda (float) cast'i kullanıyoruz.
        private bool SendGCodeLine(GCodeCommand cmd)
        {
            var axes = new List<short>();
            var positions = new List<float>();

            if (cmd.X.HasValue) { axes.Add(0); positions.Add((float)cmd.X.Value); }
            if (cmd.Y.HasValue) { axes.Add(1); positions.Add((float)cmd.Y.Value); }
            if (cmd.Z.HasValue) { axes.Add(2); positions.Add((float)cmd.Z.Value); }

            if (axes.Count == 0)
                return false; // G21, G90, M2 gibi hareketsiz komutlar — atlanır

            // Feed rate (F) varsa, master eksenin hızını buna göre ayarla.
            // set_Speed(short axis, float value) — Mint Help'te doğrulandı.
            // ÖNEMLİ: SPEED asla 0 olmamalı — 0 olursa hareket başlar ama hiç bitmez,
            // makine "takılı" kalır. cmd.F.Value > 0 kontrolü bu yüzden burada.
            if (cmd.F.HasValue && cmd.F.Value > 0)
            {
                mintController.set_Speed(axes[0], (float)cmd.F.Value);
            }

            if (axes.Count >= 2)
            {
                object axisArray = axes.ToArray();
                object posArray = positions.ToArray();
                mintController.VectorA((short)axes.Count, axisArray, posArray);
                mintController.DoGo1(axes[0]); // master eksen tetikler, diğerleri onunla birlikte gider
            }
            else
            {
                mintController.set_MoveA(axes[0], positions[0]);
                mintController.DoGo1(axes[0]);
            }

            return true;
        }

        // ================== GÖNDER ==================
        private async void BtnGonder_Click(object sender, EventArgs e)
        {
            if (!isConnected) { MessageBox.Show("Önce makineye bağlanmalısın.", "Uyarı"); return; }
            if (parsedCommands.Count == 0) { MessageBox.Show("Önce bir G-Code dosyası yükle.", "Uyarı"); return; }

            if (!ValidateBounds(out string hata))
            {
                MessageBox.Show("Gönderim durduruldu — güvenlik sınırı aşımı:\n\n" + hata, "Sınır Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            sendCts = new CancellationTokenSource();
            isPaused = false;
            btnGonder.Enabled = false;
            btnDuraklat.Enabled = true;
            btnDuraklat.Text = "⏸ Duraklat";
            progressBar1.Value = 0;

            try
            {
                for (int i = 0; i < parsedCommands.Count; i++)
                {
                    sendCts.Token.ThrowIfCancellationRequested();

                    while (isPaused)
                    {
                        await Task.Delay(150, sendCts.Token);
                    }

                    listBox1.SelectedIndex = i; // aktif satırı vurgula + otomatik kaydır
                    lblIlerleme.Text = $"Gönderiliyor: {i + 1}/{parsedCommands.Count}";

                    bool hareketVarMi = SendGCodeLine(parsedCommands[i]);

                    if (hareketVarMi)
                    {
                        // NOT: Şimdilik sabit bekleme kullanıyoruz. Daha sağlıklısı, Mint
                        // WorkBench Help'te "IDLE" veya "MoveStatus" arayıp gerçek C#
                        // ismini bulup, hareket bitene kadar onu poll etmek olur.
                        await Task.Delay(300, sendCts.Token);
                    }

                    progressBar1.Value = i + 1;
                }

                lblIlerleme.Text = "Tamamlandı ✓";
                MessageBox.Show("G-Code gönderimi tamamlandı.", "Bitti");
            }
            catch (OperationCanceledException)
            {
                lblIlerleme.Text = "Durduruldu";
            }
            finally
            {
                btnGonder.Enabled = true;
                btnDuraklat.Enabled = false;
            }
        }

        // ================== DURAKLAT / DEVAM ==================
        private void BtnDuraklat_Click(object sender, EventArgs e)
        {
            isPaused = !isPaused;
            btnDuraklat.Text = isPaused ? "▶ Devam Et" : "⏸ Duraklat";
            lblIlerleme.Text = isPaused ? "Duraklatıldı" : "Gönderiliyor...";
        }

        // ================== ACİL DURDUR ==================
        private void BtnAcilDurdur_Click(object sender, EventArgs e)
        {
            try
            {
                sendCts?.Cancel();
                isPaused = false;

                if (mintController != null)
                {
                    // 0, 1, 2: X, Y, Z eksenleri varsayımı — kendi ekseni numaralandırmana göre kontrol et
                    mintController.DoStop((short)0);
                    mintController.DoStop((short)1);
                    mintController.DoStop((short)2);
                }

                lblIlerleme.Text = "ACİL DURDURULDU";
                btnGonder.Enabled = true;
                btnDuraklat.Enabled = false;
                MessageBox.Show("Acil durdurma komutu gönderildi.", "Acil Dur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Acil durdurma sırasında hata: " + ex.Message, "Hata");
            }
        }
    }
}