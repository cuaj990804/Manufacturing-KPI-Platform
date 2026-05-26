using ClosedXML.Excel;
using GDIKPI.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GDIKPI.Services.ReportServices
{
    public class QualityDashboardReportService
    {
        private readonly KpisContext _context;

        public QualityDashboardReportService(KpisContext context)
        {
            _context = context;
        }

        public void CreateAreaChartQualityOverall(
            IXLWorksheet worksheet, int row, int column,
            List<string> labels, List<double> values,
            double? target,
            string title = "", string subtitle = "", int width = 1140, int height = 700)

        {
            if (labels == null || values == null || labels.Count == 0 || values.Count == 0)
            {
                using (var bitmap = new Bitmap(width, height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    using (var font = new Font("Arial", 16, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.Gray))
                    {
                        string noDataText = "Sin datos disponibles";
                        var textSize = graphics.MeasureString(noDataText, font);
                        graphics.DrawString(noDataText, font, brush,
                            (width - textSize.Width) / 2, (height - textSize.Height) / 2);
                    }

                    string tempPath = Path.GetTempFileName() + ".png";
                    try
                    {
                        bitmap.Save(tempPath, ImageFormat.Png);
                        var picture = worksheet.AddPicture(tempPath);
                        picture.MoveTo(worksheet.Cell(row, column));
                        picture.Scale(1.0);
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                }
                return;
            }

            // Ajustar márgenes
            int marginLeft = 80;
            int marginRight = 85;
            int marginTop =100;
            int marginBottom = 130;

            int chartWidth = width - marginLeft - marginRight;
            int chartHeight = height - marginTop - marginBottom;

            double minValue = 0;
            double maxValue = 100;
            double range = 100;

            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.White);

                float currentY = 15; // posición inicial desde arriba
                if (!string.IsNullOrEmpty(title))
                {
                    using (var titleFont = new Font("Arial", 18, FontStyle.Bold))
                    using (var titleBrush = new SolidBrush(Color.Black))
                    {
                        var titleSize = graphics.MeasureString(title, titleFont);
                        graphics.DrawString(title, titleFont, titleBrush,
                            (width - titleSize.Width) / 2, currentY);

                        currentY += titleSize.Height + 5; // mover posición para subtítulo
                    }
                }

                if (!string.IsNullOrEmpty(subtitle))
                {
                    using (var subtitleFont = new Font("Arial", 12, FontStyle.Italic))
                    using (var subtitleBrush = new SolidBrush(Color.DimGray))
                    {
                        var subtitleSize = graphics.MeasureString(subtitle, subtitleFont);
                        graphics.DrawString(subtitle, subtitleFont, subtitleBrush,
                            (width - subtitleSize.Width) / 2, currentY);

                        currentY += subtitleSize.Height + 10; // espacio antes del gráfico
                    }
                }



                if (target.HasValue)
                {
                    float yTarget = marginTop + (float)((maxValue - target.Value) / range * chartHeight);

                    using (var targetPen = new Pen(Color.Red, 2) { DashStyle = DashStyle.Dash })
                    {
                        graphics.DrawLine(targetPen, marginLeft, yTarget, marginLeft + chartWidth, yTarget);
                    }

                    using (var font = new Font("Arial", 12, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.Red))
                    {
                        string text = $"Target: {target.Value:F1}%";
                        var textSize = graphics.MeasureString(text, font);
                        graphics.DrawString(text, font, brush, marginLeft + chartWidth - textSize.Width, yTarget - textSize.Height - 2);
                    }
                }
                // Puntos de datos
                var dataPoints = new List<PointF>();

                if (values.Count == 1)
                {
                    // Para un solo valor, crear una línea horizontal que atraviese todo el gráfico
                    float y = marginTop + (float)((maxValue - values[0]) / range * chartHeight);
                    dataPoints.Add(new PointF(marginLeft, y));
                    dataPoints.Add(new PointF(marginLeft + chartWidth, y));
                }
                else
                {
                    // Para múltiples valores, usar la lógica original
                    for (int i = 0; i < values.Count; i++)
                    {
                        float x = marginLeft + (float)(i * chartWidth / (values.Count - 1.0f));
                        float y = marginTop + (float)((maxValue - values[i]) / range * chartHeight);
                        dataPoints.Add(new PointF(x, y));
                    }
                }

                if (dataPoints.Count > 0)
                {
                    // Área de relleno
                    var areaPoints = new List<PointF>();
                    areaPoints.AddRange(dataPoints);
                    areaPoints.Add(new PointF(dataPoints.Last().X, marginTop + chartHeight));
                    areaPoints.Add(new PointF(dataPoints.First().X, marginTop + chartHeight));

                    using (var areaBrush = new SolidBrush(Color.FromArgb(100, 188, 223, 246)))
                    {
                        graphics.FillPolygon(areaBrush, areaPoints.ToArray());
                    }

                    // Línea principal
                    using (var linePen = new Pen(Color.FromArgb(0, 82, 155), 3))
                    {
                        if (dataPoints.Count > 1)
                        {
                            graphics.DrawLines(linePen, dataPoints.ToArray());
                        }
                        else if (values.Count == 1)
                        {
                            // Para un solo punto, dibujar línea horizontal completa
                            graphics.DrawLine(linePen, marginLeft, dataPoints[0].Y, marginLeft + chartWidth, dataPoints[0].Y);
                        }
                    }

                    // Puntos y valores
                    using (var pointBrush = new SolidBrush(Color.FromArgb(0, 82, 155)))
                    using (var pointBorder = new Pen(Color.White, 2))
                    using (var font = new Font("Arial", 12, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(Color.Black))
                    {
                        if (values.Count == 1)
                        {
                            // Para un solo valor, mostrar el punto en el centro y el texto
                            float centerX = marginLeft + chartWidth / 2;
                            float y = marginTop + (float)((maxValue - values[0]) / range * chartHeight);

                            // Punto central
                            graphics.FillEllipse(pointBrush, centerX - 5, y - 5, 10, 10);
                            graphics.DrawEllipse(pointBorder, centerX - 5, y - 5, 10, 10);

                            // Texto del valor
                            string valueText = $"{values[0]:F1}%";
                            var textSize = graphics.MeasureString(valueText, font);
                            float textY = y - textSize.Height / 2;
                            graphics.DrawString(valueText, font, textBrush, centerX + 10, textY);
                        }
                        else
                        {
                            // Para múltiples valores, usar la lógica original
                            for (int i = 0; i < dataPoints.Count; i++)
                            {
                                var point = dataPoints[i];

                                // Punto
                                graphics.FillEllipse(pointBrush, point.X - 5, point.Y - 5, 10, 10);
                                graphics.DrawEllipse(pointBorder, point.X - 5, point.Y - 5, 10, 10);

                                // Texto del valor
                                string valueText = $"{values[i]:F1}%";
                                var textSize = graphics.MeasureString(valueText, font);
                                float textY = point.Y - textSize.Height / 2;
                                graphics.DrawString(valueText, font, textBrush, point.X + 10, textY);
                            }
                        }
                    }
                }

                // Ejes
                using (var axisPen = new Pen(Color.LightGray, 1))
                {
                    graphics.DrawLine(axisPen, marginLeft, marginTop, marginLeft, marginTop + chartHeight);
                    graphics.DrawLine(axisPen, marginLeft, marginTop + chartHeight,
                        marginLeft + chartWidth, marginTop + chartHeight);
                }

                // Etiquetas eje Y
                using (var font = new Font("Arial", 12))
                using (var textBrush = new SolidBrush(Color.Gray))
                {
                    int ySteps = 5;
                    for (int i = 0; i <= ySteps; i++)
                    {
                        double value = minValue + (range * i / ySteps);
                        float y = marginTop + chartHeight - (float)(i * chartHeight / ySteps);
                        string text = $"{value:F0}%";
                        var textSize = graphics.MeasureString(text, font);
                        graphics.DrawString(text, font, textBrush,
                            marginLeft - textSize.Width - 10, y - textSize.Height / 2);

                        using (var gridPen = new Pen(Color.FromArgb(220, 220, 220)))
                        {
                            graphics.DrawLine(gridPen, marginLeft, y, marginLeft + chartWidth, y);
                        }
                    }
                }

                // Etiquetas eje X
                using (var font = new Font("Arial", 12))
                using (var textBrush = new SolidBrush(Color.Gray))
                {
                    int labelSkip = 1;
                    if (labels.Count > 8)
                    {
                        labelSkip = (int)Math.Ceiling(labels.Count / 6.0);
                    }

                    for (int i = 0; i < labels.Count; i += labelSkip)
                    {
                        float x;
                        if (values.Count == 1)
                        {
                            // Para un solo valor, centrar la etiqueta
                            x = marginLeft + chartWidth / 2;
                        }
                        else
                        {
                            x = marginLeft + (float)(i * chartWidth / (labels.Count - 1.0f));
                        }

                        string text = labels[i];
                        if (text.Length > 20) text = text.Substring(0, 8) + "...";

                        graphics.TranslateTransform(x, marginTop + chartHeight + 20);
                        graphics.RotateTransform(45);
                        graphics.DrawString(text, font, textBrush, 0, 0);
                        graphics.ResetTransform();
                    }
                }

                // Insertar imagen
                string tempPath = Path.GetTempFileName() + ".png";
                try
                {
                    bitmap.Save(tempPath, ImageFormat.Png);
                    var picture = worksheet.AddPicture(tempPath);
                    picture.MoveTo(worksheet.Cell(row, column));
                    picture.Scale(1.0);
                }
                finally
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            }
        }

        public void CreateBarChartQualityOverall(IXLWorksheet worksheet, int row, int column,
    List<string> labels, List<double> values, string title = "", string subtitle = "",
    int width = 1140, int height = 700)
        {
            if (labels == null || values == null || labels.Count == 0 || values.Count == 0)
            {
                using (var bitmap = new Bitmap(width, height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    using (var font = new Font("Arial", 16, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.Gray))
                    {
                        string noDataText = "Sin datos disponibles";
                        var textSize = graphics.MeasureString(noDataText, font);
                        graphics.DrawString(noDataText, font, brush,
                            (width - textSize.Width) / 2, (height - textSize.Height) / 2);
                    }

                    string tempPath = Path.GetTempFileName() + ".png";
                    try
                    {
                        bitmap.Save(tempPath, ImageFormat.Png);
                        var picture = worksheet.AddPicture(tempPath);
                        picture.MoveTo(worksheet.Cell(row, column));
                        picture.Scale(1.0);
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                }
                return;
            }

            // Márgenes
            int marginLeft = 80;
            int marginRight = 50;
            int marginTop = 100;
            int marginBottom = 80; // un poco más grande por etiquetas multilínea

            int chartWidth = width - marginLeft - marginRight;
            int chartHeight = height - marginTop - marginBottom;

            double minValue = 0;
            double maxValue = values.Max() < 100 ? 100 : values.Max();
            double range = maxValue - minValue;

            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.White);

                float currentY = 15; // posición inicial desde arriba
                if (!string.IsNullOrEmpty(title))
                {
                    using (var titleFont = new Font("Arial", 18, FontStyle.Bold))
                    using (var titleBrush = new SolidBrush(Color.Black))
                    {
                        var titleSize = graphics.MeasureString(title, titleFont);
                        graphics.DrawString(title, titleFont, titleBrush,
                            (width - titleSize.Width) / 2, currentY);

                        currentY += titleSize.Height + 5; // mover posición para subtítulo
                    }
                }

                if (!string.IsNullOrEmpty(subtitle))
                {
                    using (var subtitleFont = new Font("Arial", 12, FontStyle.Italic))
                    using (var subtitleBrush = new SolidBrush(Color.DimGray))
                    {
                        var subtitleSize = graphics.MeasureString(subtitle, subtitleFont);
                        graphics.DrawString(subtitle, subtitleFont, subtitleBrush,
                            (width - subtitleSize.Width) / 2, currentY);

                        currentY += subtitleSize.Height + 10; // espacio antes del gráfico
                    }
                }

                // Ejes
                using (var axisPen = new Pen(Color.Black, 1))
                {
                    graphics.DrawLine(axisPen, marginLeft, marginTop, marginLeft, marginTop + chartHeight); // Y
                    graphics.DrawLine(axisPen, marginLeft, marginTop + chartHeight,
                        marginLeft + chartWidth, marginTop + chartHeight); // X
                }

                // Calcular ancho de cada barra
                int barCount = values.Count;
                float barWidth = (float)chartWidth / (barCount * 1.5f); // Espaciado entre barras
                float barSpacing = barWidth / 2;

                // Dibujar barras
                using (var barBrush = new SolidBrush(Color.FromArgb(0, 82, 155)))
                using (var borderPen = new Pen(Color.Black, 1))
                using (var font = new Font("Arial", 12, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.Black))
                {
                    for (int i = 0; i < barCount; i++)
                    {
                        float x = marginLeft + (i * (barWidth + barSpacing)) + barSpacing;
                        float barHeight = (float)((values[i] - minValue) / range * chartHeight);
                        float y = marginTop + chartHeight - barHeight;

                        // Dibujar barra
                        graphics.FillRectangle(barBrush, x, y, barWidth, barHeight);
                        graphics.DrawRectangle(borderPen, x, y, barWidth, barHeight);

                        // Valor arriba de la barra
                        string valueText = $"{values[i]:F1}%";
                        var textSize = graphics.MeasureString(valueText, font);
                        graphics.DrawString(valueText, font, textBrush,
                            x + (barWidth - textSize.Width) / 2,
                            y - textSize.Height - 2);

                        // Etiqueta eje X (multilínea cada 2 palabras)
                        string label = labels[i];
                        var words = label.Split(' ');
                        string formattedLabel = "";
                        for (int w = 0; w < words.Length; w++)
                        {
                            formattedLabel += words[w];
                            if ((w + 1) % 2 == 0 && w != words.Length - 1)
                                formattedLabel += "\n"; // salto de línea
                            else if (w != words.Length - 1)
                                formattedLabel += " ";
                        }

                        var labelSize = graphics.MeasureString(formattedLabel, font);
                        graphics.DrawString(formattedLabel, font, textBrush,
                            x + (barWidth - labelSize.Width) / 2,
                            marginTop + chartHeight + 5);
                    }
                }

                // Grid horizontal (Y)
                using (var font = new Font("Arial", 12))
                using (var textBrush = new SolidBrush(Color.Gray))
                {
                    int ySteps = 5;
                    for (int i = 0; i <= ySteps; i++)
                    {
                        double value = minValue + (range * i / ySteps);
                        float y = marginTop + chartHeight - (float)(i * chartHeight / ySteps);
                        string text = $"{value:F0}";
                        var textSize = graphics.MeasureString(text, font);
                        graphics.DrawString(text, font, textBrush,
                            marginLeft - textSize.Width - 10, y - textSize.Height / 2);

                        using (var gridPen = new Pen(Color.FromArgb(220, 220, 220)))
                        {
                            graphics.DrawLine(gridPen, marginLeft, y, marginLeft + chartWidth, y);
                        }
                    }
                }

                // Guardar imagen temporal e insertar
                string tempPath = Path.GetTempFileName() + ".png";
                try
                {
                    bitmap.Save(tempPath, ImageFormat.Png);
                    var picture = worksheet.AddPicture(tempPath);
                    picture.MoveTo(worksheet.Cell(row, column));
                    picture.Scale(1.0);
                }
                finally
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            }
        }

        public void CreateBarChartQualityByLine(
            IXLWorksheet worksheet, int row, int column,
            List<string> lineLabels, List<double> qualityPercentages,
            string title = "", string subtitle= "", double? target = null,
            int width = 1140, int height = 700)
        {
            if (lineLabels == null || qualityPercentages == null ||
                lineLabels.Count == 0 || qualityPercentages.Count == 0)
            {
                using (var bitmap = new Bitmap(width, height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    using (var font = new Font("Arial", 16, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.Gray))
                    {
                        string noDataText = "Sin datos disponibles";
                        var textSize = graphics.MeasureString(noDataText, font);
                        graphics.DrawString(noDataText, font, brush,
                            (width - textSize.Width) / 2, (height - textSize.Height) / 2);
                    }

                    string tempPath = Path.GetTempFileName() + ".png";
                    try
                    {
                        bitmap.Save(tempPath, ImageFormat.Png);
                        var picture = worksheet.AddPicture(tempPath);
                        picture.MoveTo(worksheet.Cell(row, column));
                        picture.Scale(1.0);
                    }
                    finally
                    {
                        if (File.Exists(tempPath))
                            File.Delete(tempPath);
                    }
                }
                return;
            }

            // Márgenes
            int marginLeft = 100;
            int marginRight = 80;
            int marginTop = 100;
            int marginBottom = 80;

            int chartWidth = width - marginLeft - marginRight;
            int chartHeight = height - marginTop - marginBottom;

            double minValue = 0;
            double maxValue = 100;
            double range = maxValue - minValue;

            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.White);

                float currentY = 15; // posición inicial desde arriba
                if (!string.IsNullOrEmpty(title))
                {
                    using (var titleFont = new Font("Arial", 18, FontStyle.Bold))
                    using (var titleBrush = new SolidBrush(Color.Black))
                    {
                        var titleSize = graphics.MeasureString(title, titleFont);
                        graphics.DrawString(title, titleFont, titleBrush,
                            (width - titleSize.Width) / 2, currentY);

                        currentY += titleSize.Height + 5; // mover posición para subtítulo
                    }
                }

                if (!string.IsNullOrEmpty(subtitle))
                {
                    using (var subtitleFont = new Font("Arial", 12, FontStyle.Italic))
                    using (var subtitleBrush = new SolidBrush(Color.DimGray))
                    {
                        var subtitleSize = graphics.MeasureString(subtitle, subtitleFont);
                        graphics.DrawString(subtitle, subtitleFont, subtitleBrush,
                            (width - subtitleSize.Width) / 2, currentY);

                        currentY += subtitleSize.Height + 10; // espacio antes del gráfico
                    }
                }

                // Dibujar Target si existe
                if (target.HasValue)
                {
                    float yTarget = marginTop + (float)((maxValue - target.Value) / range * chartHeight);

                    using (var targetPen = new Pen(Color.Red, 2) { DashStyle = DashStyle.Dash })
                    {
                        graphics.DrawLine(targetPen, marginLeft, yTarget, marginLeft + chartWidth, yTarget);
                    }

                    using (var font = new Font("Arial", 12, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.Red))
                    {
                        string text = $"Target: {target.Value:F1}%";
                        var textSize = graphics.MeasureString(text, font);
                        graphics.DrawString(text, font, brush,
                            marginLeft + chartWidth - textSize.Width, yTarget - textSize.Height - 2);
                    }
                }

                // Ejes principales
                using (var axisPen = new Pen(Color.Black, 2))
                {
                    graphics.DrawLine(axisPen, marginLeft, marginTop, marginLeft, marginTop + chartHeight); // Y
                    graphics.DrawLine(axisPen, marginLeft, marginTop + chartHeight,
                        marginLeft + chartWidth, marginTop + chartHeight); // X
                }

                // Calcular ancho de cada barra
                int barCount = qualityPercentages.Count;
                float totalBarWidth = (float)chartWidth / (barCount * 1.3f);
                float barWidth = totalBarWidth * 0.8f;
                float barSpacing = totalBarWidth * 0.2f;

                var barColor = Color.FromArgb(0, 82, 155);

                // Dibujar barras
                using (var barBrush = new SolidBrush(barColor))
                using (var borderPen = new Pen(Color.Black, 1))
                using (var font = new Font("Arial", 11, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.Black))
                using (var labelFont = new Font("Arial", 10))
                using (var labelBrush = new SolidBrush(Color.DarkBlue))
                {
                    for (int i = 0; i < barCount; i++)
                    {
                        float x = marginLeft + (i * (barWidth + barSpacing)) + barSpacing;
                        float barHeight = (float)((qualityPercentages[i] - minValue) / range * chartHeight);
                        float y = marginTop + chartHeight - barHeight;

                        graphics.FillRectangle(barBrush, x, y, barWidth, barHeight);
                        graphics.DrawRectangle(borderPen, x, y, barWidth, barHeight);

                        // Valor encima de la barra
                        string valueText = $"{qualityPercentages[i]:F1}%";
                        var textSize = graphics.MeasureString(valueText, font);
                        graphics.DrawString(valueText, font, textBrush,
                            x + (barWidth - textSize.Width) / 2,
                            y - textSize.Height - 5);

                        // Etiqueta de la línea
                        string lineLabel = lineLabels[i];
                        if (lineLabel.Length > 15)
                        {
                            lineLabel = lineLabel.Substring(0, 12) + "...";
                        }

                        var labelSize = graphics.MeasureString(lineLabel, labelFont);

                        if (barCount > 8)
                        {
                            graphics.TranslateTransform(x + barWidth / 2, marginTop + chartHeight + 15);
                            graphics.RotateTransform(45);
                            graphics.DrawString(lineLabel, labelFont, labelBrush, 0, 0);
                            graphics.ResetTransform();
                        }
                        else
                        {
                            graphics.DrawString(lineLabel, labelFont, labelBrush,
                                x + (barWidth - labelSize.Width) / 2,
                                marginTop + chartHeight + 10);
                        }
                    }
                }

                // Grid horizontal
                using (var gridFont = new Font("Arial", 11))
                using (var gridTextBrush = new SolidBrush(Color.Gray))
                using (var gridPen = new Pen(Color.FromArgb(200, 200, 200), 1))
                {
                    int ySteps = 10;
                    for (int i = 0; i <= ySteps; i++)
                    {
                        double value = minValue + (range * i / ySteps);
                        float y = marginTop + chartHeight - (float)(i * chartHeight / ySteps);
                        string text = $"{value:F0}%";
                        var textSize = graphics.MeasureString(text, gridFont);

                        graphics.DrawString(text, gridFont, gridTextBrush,
                            marginLeft - textSize.Width - 15, y - textSize.Height / 2);

                        if (i > 0)
                        {
                            graphics.DrawLine(gridPen, marginLeft + 1, y, marginLeft + chartWidth, y);
                        }
                    }
                }

                // Guardar imagen e insertar
                string tempPath = Path.GetTempFileName() + ".png";
                try
                {
                    bitmap.Save(tempPath, ImageFormat.Png);
                    var picture = worksheet.AddPicture(tempPath);
                    picture.MoveTo(worksheet.Cell(row, column));
                    picture.Scale(1.0);
                }
                finally
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            }
        }







    }

}

