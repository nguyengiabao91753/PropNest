using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropNest.Api.Contracts.Listings;
using PropNest.Application.Listings;

namespace PropNest.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/locations")]
public sealed class LocationsController(IVietnamAddressService addressService) : ControllerBase
{
    [HttpGet("provinces")]
    public async Task<ActionResult<IReadOnlyList<ProvinceDto>>> GetProvinces(
        [FromQuery] string mode = "post",
        CancellationToken cancellationToken = default)
    {
        var provinces = await addressService.GetProvincesAsync(mode, cancellationToken);
        return Ok(provinces);
    }

    [HttpGet("provinces/{provinceCode:int}/districts")]
    public async Task<ActionResult<IReadOnlyList<DistrictDto>>> GetDistrictsByProvince(
        int provinceCode,
        CancellationToken cancellationToken = default)
    {
        var districts = await addressService.GetDistrictsByProvinceAsync(provinceCode, cancellationToken);
        return Ok(districts);
    }

    [HttpGet("provinces/{provinceCode:int}/wards")]
    public async Task<ActionResult<IReadOnlyList<WardDto>>> GetWardsByProvince(
        int provinceCode,
        CancellationToken cancellationToken = default)
    {
        var wards = await addressService.GetWardsByProvinceAsync(provinceCode, cancellationToken);
        return Ok(wards);
    }

    [HttpGet("districts/{districtCode:int}/wards")]
    public async Task<ActionResult<IReadOnlyList<WardDto>>> GetWardsByDistrict(
        int districtCode,
        CancellationToken cancellationToken = default)
    {
        var wards = await addressService.GetWardsByDistrictAsync(districtCode, cancellationToken);
        return Ok(wards);
    }

    [HttpGet("convert-legacy")]
    public async Task<ActionResult<LegacyWardMappingDto>> ConvertLegacyWard(
        [FromQuery] int? legacyWardCode,
        [FromQuery] string? legacyWardName,
        [FromQuery] int? legacyProvinceCode,
        CancellationToken cancellationToken = default)
    {
        var mapping = await addressService.ConvertLegacyWardAsync(
            legacyWardCode,
            legacyWardName,
            legacyProvinceCode,
            cancellationToken);

        return mapping is null ? NotFound() : Ok(mapping);
    }

    [HttpPost("parse-address")]
    public async Task<ActionResult<ParsedAddressResultDto>> ParseAddress(
        [FromBody] ParseAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await addressService.ParseAndSuggestAddressAsync(request.RawAddress, cancellationToken);
        return Ok(result);
    }
}
