using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

using ePatientApi.Interfaces;
using ePatientApi.Models;

namespace ePatientApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class HomepageController : ControllerBase
    {
        private readonly IHomepageData _homepageDataService;
        private readonly IJwtToken _jwtToken;

        /// <summary>
        /// Initializes a new instance of <see cref="HomepageController"/>.
        /// </summary>
        public HomepageController(IHomepageData homepageDataService, IJwtToken jwtToken)
        {
            _homepageDataService = homepageDataService ?? throw new ArgumentNullException(nameof(homepageDataService));
            _jwtToken = jwtToken ?? throw new ArgumentNullException(nameof(jwtToken));
        }

        /// <summary>
        /// Retrieves homepage data for the authenticated user.
        /// </summary>
        /// <returns>Homepage data including patient information and appointments.</returns>
        [HttpGet]
        public async Task<ActionResult<HomepageData>> GetHomepageData()
        {
            var token = _jwtToken.ExtractTokenFromHeader(Request);
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Missing token.");
            }

            var jwt = _jwtToken.ParseJwtToken(token);
            if (jwt == null)
            {
                return BadRequest("Invalid token.");
            }

            var data = await _homepageDataService.GetHomepageDataAsync();
            return Ok(data);
        }

        /// <summary>
        /// Exports patient data to PDF format.
        /// </summary>
        /// <returns>PDF file containing patient report.</returns>
        [HttpGet("export-pdf")]
        public async Task<IActionResult> ExportToPdf()
        {
            var token = _jwtToken.ExtractTokenFromHeader(Request);
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest("Missing token.");
            }

            var jwt = _jwtToken.ParseJwtToken(token);
            if (jwt == null)
            {
                return BadRequest("Invalid token.");
            }

            var data = await _homepageDataService.GetHomepageDataAsync();
            using var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Arial", 12, XFontStyle.Regular);
            var titleFont = new XFont("Arial", 16, XFontStyle.Bold);
            var yPosition = 40;
            gfx.DrawString("Patient Report", titleFont, XBrushes.Black,
                new XRect(40, yPosition, page.Width, page.Height), XStringFormats.TopLeft);

            yPosition = DrawPatientInfo(gfx, font, data, yPosition + 30);

            gfx.DrawString("Recent Diagnoses:", font, XBrushes.Black,
                new XRect(40, yPosition, page.Width, page.Height), XStringFormats.TopLeft);
            yPosition += 20;

            foreach (var diagnosis in data.RecentDiagnoses)
            {
                gfx.DrawString($"- {diagnosis}", font, XBrushes.Black,
                    new XRect(60, yPosition, page.Width, page.Height), XStringFormats.TopLeft);
                yPosition += 20;
            }

            using var stream = new MemoryStream();
            document.Save(stream, false);
            stream.Position = 0;

            return File(stream.ToArray(), "application/pdf", "PatientReport.pdf");
        }

        /// <summary>
        /// Helper method to draw patient information in PDF.
        /// </summary>
        private static int DrawPatientInfo(XGraphics gfx, XFont font, HomepageData data, int startY)
        {
            var y = startY;
            gfx.DrawString($"Name: {data.PatientName}", font, XBrushes.Black,
                new XRect(40, y, gfx.PdfPage.Width, gfx.PdfPage.Height), XStringFormats.TopLeft);
            y += 20;

            gfx.DrawString($"Age: {data.Age}", font, XBrushes.Black,
                new XRect(40, y, gfx.PdfPage.Width, gfx.PdfPage.Height), XStringFormats.TopLeft);
            y += 20;

            gfx.DrawString($"Last Visit: {data.LastVisitDate}", font, XBrushes.Black,
                new XRect(40, y, gfx.PdfPage.Width, gfx.PdfPage.Height), XStringFormats.TopLeft);
            y += 20;

            gfx.DrawString($"Upcoming Appointment: {data.UpcomingAppointment}", font, XBrushes.Black,
                new XRect(40, y, gfx.PdfPage.Width, gfx.PdfPage.Height), XStringFormats.TopLeft);

            return y + 20;
        }
    }
}