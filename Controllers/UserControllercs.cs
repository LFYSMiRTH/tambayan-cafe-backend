using Microsoft.AspNetCore.Mvc;
using TambayanCafeSystem.Models;
using TambayanCafeSystem.Services;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        // ✅ Inject UserService via constructor
        public UserController(UserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public ActionResult<List<User>> Get() => _userService.Get();

        [HttpPost("register")]
        public ActionResult<User> Register([FromBody] User user)
        {
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
    }
}