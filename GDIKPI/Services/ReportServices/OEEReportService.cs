using ClosedXML.Excel;
using GDIKPI.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GDIKPI.Services.ReportServices
{
    public class OEEReportService
    {
        private readonly KpisContext _context;
        public OEEReportService(KpisContext context)
        {
            _context = context;
        }
        public void CreateImageProgressBar(IXLWorksheet worksheet, int row, int column, double value, double maxValue, int width, int height)
        {
            double percentage = Math.Max(0, Math.Min(1, value / maxValue)); // Clamp entre 0 y 1

            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                // Habilitar anti-aliasing para bordes más suaves
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // Fondo con color gris muy claro (moderno)
                using (var backgroundBrush = new SolidBrush(Color.FromArgb(248, 249, 250)))
                {
                    graphics.FillRectangle(backgroundBrush, 0, 0, width, height);
                }

                // Radio para bordes redondeados
                int cornerRadius = Math.Min(height / 4, 8);

                // Crear path para el fondo con bordes redondeados
                using (var backgroundPath = CreateRoundedRectanglePath(0, 0, width, height, cornerRadius))
                {
                    // Fondo de la barra (gris claro)
                    using (var backgroundBrush = new SolidBrush(Color.FromArgb(229, 231, 235)))
                    {
                        graphics.FillPath(backgroundBrush, backgroundPath);
                    }
                }

                // Parte llena de la barra (gradiente moderno)
                if (percentage > 0)
                {
                    int filledWidth = Math.Max(1, (int)(width * percentage));

                    using (var filledPath = CreateRoundedRectanglePath(0, 0, filledWidth, height, cornerRadius))
                    {
                        // Gradiente azul moderno (horizontal, de izquierda a derecha)
                        using (var gradientBrush = new LinearGradientBrush(
                            new Rectangle(0, 0, filledWidth, height),
                            Color.FromArgb(37, 99, 235),   // Azul más oscuro (izquierda)
                            Color.FromArgb(59, 130, 246),  // Azul claro (derecha)
                            LinearGradientMode.Horizontal))
                        {
                            graphics.FillPath(gradientBrush, filledPath);
                        }
                    }

                    // Línea blanca brillante en la parte superior (optimizada para horizontal)
                    if (height >= 8 && filledWidth >= 10) // Solo si hay espacio suficiente
                    {
                        int highlightHeight = Math.Max(2, height / 4);
                        using (var highlightPath = CreateRoundedRectanglePath(2, 2, filledWidth - 4, highlightHeight, Math.Max(1, cornerRadius - 2)))
                        {
                            using (var highlightBrush = new SolidBrush(Color.FromArgb(140, 255, 255, 255))) // Blanco semitransparente
                            {
                                graphics.FillPath(highlightBrush, highlightPath);
                            }
                        }
                    }
                }

                // Borde sutil (opcional, más moderno)
                using (var borderPath = CreateRoundedRectanglePath(0, 0, width - 1, height - 1, cornerRadius))
                {
                    using (var borderPen = new Pen(Color.FromArgb(209, 213, 219), 1))
                    {
                        graphics.DrawPath(borderPen, borderPath);
                    }
                }

                // Guardar imagen temporal
                string tempPath = Path.GetTempFileName() + ".png";
                bitmap.Save(tempPath, ImageFormat.Png);

                // Insertar imagen en Excel
                var picture = worksheet.AddPicture(tempPath);
                picture.MoveTo(worksheet.Cell(row, column));
                picture.Scale(1.0);

                // Limpiar archivo temporal
                File.Delete(tempPath);
            }
        }

        // Función auxiliar para crear paths con bordes redondeados
        private GraphicsPath CreateRoundedRectanglePath(int x, int y, int width, int height, int radius)
        {
            var path = new GraphicsPath();

            if (radius <= 0 || width <= 0 || height <= 0)
            {
                path.AddRectangle(new Rectangle(x, y, width, height));
                return path;
            }

            // Ajustar el radio si es muy grande para el rectángulo
            radius = Math.Min(radius, Math.Min(width / 2, height / 2));

            int diameter = radius * 2;

            // Esquina superior izquierda
            path.AddArc(x, y, diameter, diameter, 180, 90);
            // Línea superior
            path.AddLine(x + radius, y, x + width - radius, y);
            // Esquina superior derecha
            path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
            // Línea derecha
            path.AddLine(x + width, y + radius, x + width, y + height - radius);
            // Esquina inferior derecha
            path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
            // Línea inferior
            path.AddLine(x + width - radius, y + height, x + radius, y + height);
            // Esquina inferior izquierda
            path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
            // Línea izquierda
            path.AddLine(x, y + height - radius, x, y + radius);

            path.CloseFigure();
            return path;
        }




        // Método optimizado para CreateAreaChart en OEEReportService
        public void CreateAreaChart(IXLWorksheet worksheet, int row, int column,
    List<string> labels, List<double> values, string title = "",
    int width = 1350, int height = 570)
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

            // Ajustar tamaños
            int marginLeft = 80;
            int marginRight = 50;
            int marginTop = !string.IsNullOrEmpty(title) ? 60 : 40;
            int marginBottom = 85;

            int chartWidth = width - marginLeft - marginRight;
            int chartHeight = height - marginTop - marginBottom;

            // Eje Y fijo de 0 a 100%
            double minValue = 0;
            double maxValue = 100;
            double range = 100;

            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.White);

                // Título
                if (!string.IsNullOrEmpty(title))
                {
                    using (var titleFont = new Font("Arial", 18, FontStyle.Bold))
                    using (var titleBrush = new SolidBrush(Color.Black))
                    {
                        var titleSize = graphics.MeasureString(title, titleFont);
                        graphics.DrawString(title, titleFont, titleBrush,
                            (width - titleSize.Width) / 2, 15);
                    }
                }

                // Puntos de datos
                var dataPoints = new List<PointF>();

                // Si solo hay un dato, crear dos puntos (inicio y fin) para que se vea toda la gráfica
                if (values.Count == 1)
                {
                    float y = marginTop + (float)((maxValue - values[0]) / range * chartHeight);
                    dataPoints.Add(new PointF(marginLeft, y));
                    dataPoints.Add(new PointF(marginLeft + chartWidth, y));
                }
                else
                {
                    for (int i = 0; i < values.Count; i++)
                    {
                        float x = marginLeft + (float)(i * chartWidth / Math.Max(1, values.Count - 1.0));
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

                    using (var areaBrush = new SolidBrush(Color.FromArgb(100, 188, 223, 246))) // azul claro
                    {
                        graphics.FillPolygon(areaBrush, areaPoints.ToArray());
                    }

                    // Línea principal
                    if (dataPoints.Count > 1)
                    {
                        using (var linePen = new Pen(Color.FromArgb(0, 82, 155), 3)) // azul marino
                        {
                            graphics.DrawLines(linePen, dataPoints.ToArray());
                        }
                    }

                    // Puntos y valores
                    using (var pointBrush = new SolidBrush(Color.FromArgb(0, 82, 155)))
                    using (var pointBorder = new Pen(Color.White, 2))
                    using (var font = new Font("Arial", 12, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(Color.Black))
                    {
                        // Si solo hay un valor, dibujar un punto en el centro
                        if (values.Count == 1)
                        {
                            float centerX = marginLeft + (chartWidth / 2);
                            float y = marginTop + (float)((maxValue - values[0]) / range * chartHeight);

                            // Punto central
                            graphics.FillEllipse(pointBrush, centerX - 5, y - 5, 10, 10);
                            graphics.DrawEllipse(pointBorder, centerX - 5, y - 5, 10, 10);

                            // Texto encima
                            string valueText = $"{values[0]:F1}%";
                            var textSize = graphics.MeasureString(valueText, font);
                            graphics.DrawString(valueText, font, textBrush,
                                centerX + 15 - textSize.Width / 2, y - textSize.Height - 15);
                        }
                        else
                        {
                            // Para múltiples valores, dibujar todos los puntos
                            for (int i = 0; i < dataPoints.Count; i++)
                            {
                                var point = dataPoints[i];

                                // Punto
                                graphics.FillEllipse(pointBrush, point.X - 5, point.Y - 5, 10, 10);
                                graphics.DrawEllipse(pointBorder, point.X - 5, point.Y - 5, 10, 10);

                                // Texto encima
                                string valueText = $"{values[i]:F1}%";
                                var textSize = graphics.MeasureString(valueText, font);
                                graphics.DrawString(valueText, font, textBrush,
                                    point.X + 15 - textSize.Width / 2, point.Y - textSize.Height - 15);
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

                        using (var gridPen = new Pen(Color.FromArgb(220, 220, 220))) // gris suave
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
                        float x = marginLeft + (float)(i * chartWidth / Math.Max(1, labels.Count - 1.0));
                        string text = labels[i];

                        if (text.Length > 10)
                            text = text.Substring(0, 8) + "...";

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





    }




}