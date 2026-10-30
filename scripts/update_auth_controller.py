# -*- coding: utf-8 -*-
with open('src/PropNest.Api/Controllers/AuthController.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target = """    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.GenerateAccessToken(user, roles);

        return Ok(new LoginResponse(accessToken));
    }"""

replacement = """    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return StatusCode(StatusCodes.Status423Locked, new { message = "Tài khoản tạm thời bị khóa do nhập sai mật khẩu quá 5 lần. Vui lòng thử lại sau 15 phút." });
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.GenerateAccessToken(user, roles);

        return Ok(new LoginResponse(accessToken));
    }"""

norm_text = text.replace('\r\n', '\n')
norm_target = target.replace('\r\n', '\n')
norm_replacement = replacement.replace('\r\n', '\n')

if norm_target in norm_text:
    new_text = norm_text.replace(norm_target, norm_replacement, 1)
    if '\r\n' in text:
        new_text = new_text.replace('\n', '\r\n')
    with open('src/PropNest.Api/Controllers/AuthController.cs', 'w', encoding='utf-8') as f:
        f.write(new_text)
    print("Updated AuthController.cs successfully!")
else:
    print("Target not found!")
