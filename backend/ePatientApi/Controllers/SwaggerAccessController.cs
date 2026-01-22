using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace ePatientApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SwaggerAccessController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public SwaggerAccessController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("swagger-access")]
        public IActionResult GetSwaggerAccess()
        {
            if (HttpContext.Session.GetString("swagger_unlocked") == "true")
            {
                return Redirect("/swagger");
            }

            var html = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Swagger Access</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(to top left, #000000, #007474);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }

        .container {
            background: white;
            border-radius: 16px;
            box-shadow: 0 6px 6px rgba(0, 0, 0, 0.3);
            padding: 48px;
            max-width: 440px;
            width: 100%;
            animation: slideUp 0.5s ease-out;
        }

        @keyframes slideUp {
            from {
                opacity: 0;
                transform: translateY(30px);
            }
            to {
                opacity: 1;
                transform: translateY(0);
            }
        }

        h1 {
            color: #1a1a1a;
            font-size: 28px;
            font-weight: 600;
            margin-bottom: 12px;
            text-align: center;
        }

        p {
            color: #666;
            font-size: 16px;
            margin-bottom: 32px;
            text-align: center;
            line-height: 1.5;
        }

        form {
            display: flex;
            flex-direction: column;
            gap: 20px;
        }

        .input-group {
            position: relative;
        }

        input[type=""password""] {
            width: 100%;
            padding: 14px 16px;
            font-size: 15px;
            border: 2px solid #e1e8ed;
            border-radius: 8px;
            transition: all 0.3s ease;
            font-family: inherit;
            background: #f8f9fa;
        }

        input[type=""password""]:focus {
            outline: none;
            border-color: #007474;
            background: white;
            box-shadow: 0 0 0 4px rgba(0, 116, 116, 0.1);
        }

        input[type=""password""]::placeholder {
            color: #999;
        }

        button {
            width: 100%;
            padding: 14px 24px;
            font-size: 16px;
            font-weight: 600;
            color: white;
            background: #007474;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            transition: all 0.3s ease;
            font-family: inherit;
        }

        button:hover {
            transform: translateY(-2px);
            box-shadow: 0 2px 2px rgba(0, 116, 116, 0.4);
            background: #009090;
        }

        button:active {
            transform: translateY(0);
        }

        .lock-icon {
            text-align: center;
            margin-bottom: 24px;
            font-size: 48px;
            color: #007474;
        }

        @media (max-width: 480px) {
            .container {
                padding: 32px 24px;
            }

            h1 {
                font-size: 24px;
            }

            p {
                font-size: 14px;
            }
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""lock-icon"">🔒</div>
        <h1>Swagger Access Locked</h1>
        <p>Enter the password to unlock Swagger API documentation.</p>
        <form method=""post"">
            <div class=""input-group"">
                <input type=""password"" name=""password"" placeholder=""Enter password"" required autofocus />
            </div>
            <button type=""submit"">Unlock Swagger</button>
        </form>
    </div>
</body>
</html>";
            return Content(html, "text/html");
        }

        [HttpPost("swagger-access")]
        public IActionResult PostSwaggerAccess([FromForm] string password)
        {
            var correctPassword = _configuration["AppSwagger:Password"];
            if (string.IsNullOrEmpty(correctPassword))
            {
                return BadRequest("Swagger password not configured.");
            }

            if (password == correctPassword)
            {
                HttpContext.Session.SetString("swagger_unlocked", "true");
                return Redirect("/swagger");
            }
            else
            {
                var html = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Swagger Access</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(to top left, #000000, #007474);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }

        .container {
            background: white;
            border-radius: 16px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            padding: 48px;
            max-width: 440px;
            width: 100%;
            animation: slideUp 0.5s ease-out;
        }

        @keyframes slideUp {
            from {
                opacity: 0;
                transform: translateY(30px);
            }
            to {
                opacity: 1;
                transform: translateY(0);
            }
        }

        .logo {
            width: 60px;
            height: 60px;
            margin: 0 auto 24px;
            background: linear-gradient(135deg, #85ea2d 0%, #5cb85c 100%);
            border-radius: 12px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 32px;
            font-weight: bold;
            color: white;
        }

        h1 {
            color: #1a1a1a;
            font-size: 28px;
            font-weight: 600;
            margin-bottom: 12px;
            text-align: center;
        }

        p {
            color: #666;
            font-size: 16px;
            margin-bottom: 32px;
            text-align: center;
            line-height: 1.5;
        }

        .error {
            color: #e74c3c;
            font-size: 14px;
            margin-top: -20px;
            margin-bottom: 20px;
            text-align: center;
        }

        form {
            display: flex;
            flex-direction: column;
            gap: 20px;
        }

        .input-group {
            position: relative;
        }

        input[type=""password""] {
            width: 100%;
            padding: 14px 16px;
            font-size: 15px;
            border: 2px solid #e1e8ed;
            border-radius: 8px;
            transition: all 0.3s ease;
            font-family: inherit;
            background: #f8f9fa;
        }

        input[type=""password""]:focus {
            outline: none;
            border-color: #007474;
            background: white;
            box-shadow: 0 0 0 4px rgba(0, 116, 116, 0.1);
        }

        input[type=""password""]::placeholder {
            color: #999;
        }

        button {
            width: 100%;
            padding: 14px 24px;
            font-size: 16px;
            font-weight: 600;
            color: white;
            background: #007474;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            transition: all 0.3s ease;
            font-family: inherit;
        }

        button:hover {
            transform: translateY(-2px);
            box-shadow: 0 8px 20px rgba(0, 116, 116, 0.4);
            background: #009090;
        }

        button:active {
            transform: translateY(0);
        }

        .lock-icon {
            text-align: center;
            margin-bottom: 24px;
            font-size: 48px;
            color: #007474;
        }

        @media (max-width: 480px) {
            .container {
                padding: 32px 24px;
            }

            h1 {
                font-size: 24px;
            }

            p {
                font-size: 14px;
            }
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""lock-icon"">🔒</div>
        <h1>Swagger Access Locked</h1>
        <p>Enter the password to unlock Swagger API documentation.</p>
        <p class=""error"">Incorrect password. Try again.</p>
        <form method=""post"">
            <div class=""input-group"">
                <input type=""password"" name=""password"" placeholder=""Enter password"" required autofocus />
            </div>
            <button type=""submit"">Unlock Swagger</button>
        </form>
    </div>
</body>
</html>";
                return Content(html, "text/html");
            }
        }
    }
}