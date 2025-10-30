using Microsoft.AspNetCore.Mvc;
using TambayanCafeSystem.Models;
using TambayanCafeSystem.Services;
using System;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public ActionResult<List<User>> Get() => _userService.Get();

        [HttpGet("check-username")]
        public IActionResult CheckUsername([FromQuery] string username)
        {
            var user = _userService.GetByUsername(username);
            if (user != null)
                return Conflict("Username already exists");
            return Ok("Username is available");
        }

        [HttpGet("check-email")]
        public IActionResult CheckEmail([FromQuery] string email)
        {
            var user = _userService.GetByEmail(email);
            if (user != null)
                return Conflict("Email already exists");
            return Ok("Email is available");
        }

        [HttpPost("register")]
        public ActionResult<User> Register([FromBody] User user)
        {
            if (string.IsNullOrEmpty(user.Password) || user.Password.Length < 8)
                return BadRequest("Password must be at least 8 characters long");

            if (_userService.GetByUsername(user.Username) != null)
                return Conflict("Username already exists");

            if (_userService.GetByEmail(user.Email) != null)
                return Conflict("Email already exists");

            user.Id = null;
            var createdUser = _userService.Create(user);
            return Ok(createdUser);
        }

        [HttpPost("login")]
        public ActionResult<User> Login([FromBody] User loginUser)
        {
            var user = _userService.GetByUsername(loginUser.Username);
            if (user == null || user.Password != loginUser.Password)
                return Unauthorized("Invalid username or password");

            return Ok(user);
        }

        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword([FromBody] dynamic request)
        {
            string email = request.email;
            var user = _userService.GetByEmail(email);
            if (user == null)
                return NotFound("Email not found");

            string resetCode = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
            _userService.SaveResetCode(email, resetCode);

            return Ok(new { message = "Reset code sent to email", code = resetCode });
        }

        [HttpPost("verify-reset-code")]
        public IActionResult VerifyResetCode([FromBody] dynamic request)
        {
            string email = request.email;
            string code = request.code;

            if (_userService.VerifyResetCode(email, code))
                return Ok("Code verified successfully");

            return BadRequest("Invalid or expired code");
        }

        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] dynamic request)
        {
            string email = request.email;
            string newPassword = request.newPassword;

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
                return BadRequest("Password must be at least 8 characters long");

            bool success = _userService.ResetPassword(email, newPassword);
            if (!success)
                return NotFound("Email not found or reset failed");

            return Ok("Password has been reset successfully");
        }
    }
}
