using Microsoft.AspNetCore.Mvc;
using ePatientApi.Interfaces;

namespace ePatientApi.Controllers
{
    [ApiController]
    [Route("api/insurance")]
    public sealed class InsuranceController : ControllerBase
    {
        private readonly IVsZPService myVsZPService;
        private readonly IUnionService myUnionService;
        private readonly IDoveraService myDoveraService;

        public InsuranceController(IVsZPService vszp, IUnionService union, IDoveraService dovera)
        {
            myUnionService = union;
            myVsZPService = vszp;
            myDoveraService = dovera;
        }

        [HttpPost("vszp")]
        public async Task<IActionResult> CheckVSZP([FromBody] CheckInsuranceRequest request, CancellationToken cancelToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.birthNumber))
            {
                return BadRequest(new
                {
                    message = "Birth number is required."
                });
            }

            var rC = request.birthNumber.Trim();
            var date = request.date ?? DateTime.Today;

            bool Check;

            Check = await myVsZPService.CheckAsync(rC, date, cancelToken);

            return Ok(new CheckSingleResponse(Check ? 1 : 0));
        }

        [HttpPost("union")]
        public async Task<IActionResult> CheckUnion([FromBody] CheckInsuranceRequest request, CancellationToken cancelToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.birthNumber))
            {
                return BadRequest(new
                {
                    message = "Birth number is required."
                });
            }

            var rC = request.birthNumber.Trim();
            var date = request.date ?? DateTime.Today;

            bool Check;

            Check = await myUnionService.CheckAsync(rC, date, cancelToken);

            return Ok(new CheckSingleResponse(Check ? 1 : 0));
        }

        [HttpPost("dovera")]
        public async Task<IActionResult> CheckDovera([FromBody] CheckInsuranceRequest request, CancellationToken cancelToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.birthNumber))
            {
                return BadRequest(new
                {
                    message = "Birth number is required."
                });
            }

            var rC = request.birthNumber.Trim();
            var date = request.date ?? DateTime.Today;

            bool Check;

            Check = await myDoveraService.CheckAsync(rC, date, cancelToken);

            return Ok(new CheckSingleResponse(Check ? 1 : 0));
        }

        [HttpPost("check-all")]
        public async Task<IActionResult> CheckAll([FromBody] CheckInsuranceRequest request, CancellationToken cancelToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.birthNumber))
            {
                return BadRequest(new
                {
                    message = "Birth number is required."
                });
            }

            var rC = request.birthNumber.Trim();
            var date = request.date ?? DateTime.Today;

            var vszpTask = myVsZPService.CheckAsync(rC, date, cancelToken);
            var unionTask = myUnionService.CheckAsync(rC, date, cancelToken);
            var doveraTask = myDoveraService.CheckAsync(rC, date, cancelToken);

            await Task.WhenAll(vszpTask, unionTask, doveraTask);

            var result = new CheckAllResponses(
                vszp: vszpTask.Result ? 1 : 0,
                union: unionTask.Result ? 1 : 0,
                dovera: doveraTask.Result ? 1 : 0
            );

            return Ok(result);
        }

        public record CheckInsuranceRequest(string birthNumber, DateTime? date);
        public record CheckSingleResponse(int ok);
        public record CheckAllResponses(int vszp, int union, int dovera);
    }
}