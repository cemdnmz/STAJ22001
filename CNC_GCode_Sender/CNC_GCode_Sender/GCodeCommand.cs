using System;
using System.Globalization;

namespace CNC_GCode_Sender
{
    public class GCodeCommand
    {
        public string RawLine { get; set; }
        public string CommandType { get; set; }

        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
        public double? F { get; set; } // Hız (Feedrate) için eklendi

        public GCodeCommand(string line)
        {
            RawLine = line;
            ParseLine();
        }

        private void ParseLine()
        {
            // 1. Önce satırdaki yorum (;) işaretini bulalım
            int commentIndex = RawLine.IndexOf(';');

            // 2. Yorum varsa onu ve sonrasını kesip atalım, yoksa satırın tamamını alalım
            string cleanLine = commentIndex >= 0 ? RawLine.Substring(0, commentIndex) : RawLine;

            // 3. Sağdaki soldaki boşlukları temizleyelim
            cleanLine = cleanLine.Trim();

            // 4. Eğer geriye sadece boş bir satır kaldıysa (tamamı yorumsa) işlem yapmadan çıkalım
            if (string.IsNullOrWhiteSpace(cleanLine))
                return;

            // 5. Tertemiz kalan komutu boşluklardan parçalayalım
            string[] parts = cleanLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                char letter = char.ToUpper(part[0]);
                string valueStr = part.Substring(1);

                double parsedValue = 0;
                bool isNumber = double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedValue);

                if (letter == 'G' || letter == 'M')
                {
                    CommandType = letter + valueStr;
                }
                else if (isNumber)
                {
                    if (letter == 'X') X = parsedValue;
                    else if (letter == 'Y') Y = parsedValue;
                    else if (letter == 'Z') Z = parsedValue;
                    else if (letter == 'F') F = parsedValue;
                }
            }
        }
    }
}