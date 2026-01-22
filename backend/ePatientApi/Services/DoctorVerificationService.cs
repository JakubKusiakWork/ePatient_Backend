using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Text;
using System.Globalization;
using HtmlAgilityPack;
using ePatientApi.Models;

namespace ePatientApi.Services
{
    /// <summary>
    /// Service for verifying doctors against the LEKOM registry.
    /// </summary>
    public class DoctorVerificationService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://lekom.sk/register-lekarov-slk";

        private static string NormalizeForComparison(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var cleaned = RemoveDiacritics(input).ToLowerInvariant();
            return cleaned;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        public DoctorVerificationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<DoctorVerificationResult> VerifyDoctorAsync(string firstName, string lastName, int specializationId)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                return new DoctorVerificationResult { IsVerified = false, SourceUrl = BaseUrl };
            }

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["firstname"] = firstName;
            query["surname"] = lastName;
            query["job_spec"] = specializationId.ToString();

            var url = BaseUrl + "?" + query.ToString();

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var tables = doc.DocumentNode.SelectNodes("//table");
                if (tables == null)
                {
                    return new DoctorVerificationResult { IsVerified = false, SourceUrl = url };
                }

                string normFirst = NormalizeForComparison(firstName);
                string normLast = NormalizeForComparison(lastName);

                foreach (var table in tables)
                {
                    var rows = table.SelectNodes(".//tr");
                    if (rows == null) continue;

                    foreach (var row in rows)
                    {
                        var cells = row.SelectNodes(".//td|.//th");
                        if (cells == null) continue;

                        var texts = new System.Collections.Generic.List<string>();
                        foreach (var c in cells)
                        {
                            var t = HtmlEntity.DeEntitize(c.InnerText).Trim();
                            if (!string.IsNullOrEmpty(t)) texts.Add(t);
                        }

                        if (texts.Count == 0) continue;

                        var joined = string.Join(" ", texts);
                        var normJoined = NormalizeForComparison(joined);
                        bool matched = false;

                        if (normJoined.Contains(normFirst) && normJoined.Contains(normLast))
                        {
                            matched = true;
                        }
                        else
                        {
                            bool firstInCell = false, lastInCell = false;
                            foreach (var t in texts)
                            {
                                var n = NormalizeForComparison(t);
                                if (!firstInCell && n.Contains(normFirst)) firstInCell = true;
                                if (!lastInCell && n.Contains(normLast)) lastInCell = true;
                            }
                            matched = firstInCell && lastInCell;
                        }

                        if (matched)
                        {
                            string? fullName = null;
                            string? specialization = null;
                            string? lastNameExtracted = null;
                            string? firstNameExtracted = null;

                            int bothIdx = texts.FindIndex(t =>
                                NormalizeForComparison(t).Contains(normFirst) &&
                                NormalizeForComparison(t).Contains(normLast));

                            if (bothIdx >= 0)
                            {
                                fullName = texts[bothIdx];
                                if (bothIdx + 1 < texts.Count) specialization = texts[bothIdx + 1];
                            }
                            else
                            {
                                int idxFirst = texts.FindIndex(t => NormalizeForComparison(t).Contains(normFirst));
                                int idxLast = texts.FindIndex(t => NormalizeForComparison(t).Contains(normLast));

                                if (idxFirst >= 0 && idxLast >= 0)
                                {
                                    if (idxFirst <= idxLast) fullName = texts[idxFirst] + " " + texts[idxLast];
                                    else fullName = texts[idxLast] + " " + texts[idxFirst];

                                    if (idxLast + 1 < texts.Count) specialization = texts[idxLast + 1];
                                }
                                else if (idxLast >= 0)
                                {
                                    fullName = texts[idxLast];
                                    if (idxLast + 1 < texts.Count) specialization = texts[idxLast + 1];
                                }
                                else if (idxFirst >= 0)
                                {
                                    if (idxFirst + 1 < texts.Count && NormalizeForComparison(texts[idxFirst + 1]).Contains(normLast))
                                    {
                                        fullName = texts[idxFirst] + " " + texts[idxFirst + 1];
                                        if (idxFirst + 2 < texts.Count) specialization = texts[idxFirst + 2];
                                    }
                                    else
                                    {
                                        fullName = joined;
                                        if (idxFirst + 1 < texts.Count) specialization = texts[idxFirst + 1];
                                    }
                                }
                                else
                                {
                                    fullName = joined;
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(fullName))
                            {
                                var parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 0) firstNameExtracted = parts[0].Trim();
                                if (parts.Length > 1) lastNameExtracted = parts[^1].Trim();
                            }

                            return new DoctorVerificationResult
                            {
                                IsVerified = true,
                                FirstName = firstNameExtracted ?? firstName,
                                FullName = fullName ?? (firstName + " " + lastName),
                                Specialization = specialization,
                                LastName = lastNameExtracted ?? lastName,
                                SourceUrl = url
                            };
                        }
                    }
                }

                return new DoctorVerificationResult { IsVerified = false, SourceUrl = url };
            }
            catch (HttpRequestException)
            {
                return new DoctorVerificationResult { IsVerified = false, SourceUrl = url };
            }
        }
    }
}
