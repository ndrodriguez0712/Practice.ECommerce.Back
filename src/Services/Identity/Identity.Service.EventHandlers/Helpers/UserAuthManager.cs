﻿using Identity.Domain;
using Identity.Persistence.Database;
using Identity.Persistence.Database.Interfaces;
using Identity.Service.EventHandlers.Helpers.Interfaces;
using Identity.Service.EventHandlers.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Service.EventHandlers.Helpers
{
    public class UserAuthManager : IUserAuthManager
    {
        #region Variables
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEncryptionManager _encryptionService;
        #endregion

        #region Constructor        
        public UserAuthManager(IConfiguration configuration, IServiceProvider serviceProvider, IEncryptionManager encryptionService)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _encryptionService = encryptionService;
        }
        #endregion

        public async Task CreateUserAsync(ApplicationUser user)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            IUnitOfWork<ApplicationDbContext> _unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork<ApplicationDbContext>>();
            IBaseRepository<ApplicationUser> _applicationUserRepository = _unitOfWork.GetRepository<ApplicationUser>();

            await _applicationUserRepository.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<ApplicationUser> GetUserAsync(string email)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            IUnitOfWork<ApplicationDbContext> _unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork<ApplicationDbContext>>();
            IBaseRepository<ApplicationUser> _applicationUserRepository = _unitOfWork.GetRepository<ApplicationUser>();
            IBaseRepository<ApplicationUserRole> _applicationUserRoleRepository = _unitOfWork.GetRepository<ApplicationUserRole>();

            var user = await (from u in _applicationUserRepository.GetAllAsQueryable()
                    join rol in _applicationUserRoleRepository.GetAllAsQueryable()
                    on u.IdRole equals rol.Id
                    where u.Email == email
                    select u).Include(r => r.Role).FirstOrDefaultAsync();

            return user;
        }

        public bool CheckPasswordSignIn(string passwordHashed, string password)
        {
            return (passwordHashed == _encryptionService.Hash(password));
        }        

        public IdentityAccess GenerateToken(ApplicationUser user, string rol)
        {
            // Header
            var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtIssuerOptions:SecretKey"]));
            var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);
            var header = new JwtHeader(signingCredentials);

            //Claims
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.GivenName, $"{user.FirstName}_{user.LastName}"),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, rol),
                new Claim("IdUser", user.Id.ToString())
            };            

            var expiration = DateTime.UtcNow.AddDays(3);

            //Payload
            var payload = new JwtPayload(
                _configuration["JwtIssuerOptions:Issuer"],
                _configuration["JwtIssuerOptions:Audience"],
                claims,
                DateTime.Now,
                expiration);

            var jwtSecurity = new JwtSecurityToken(header, payload);

            return new IdentityAccess()
            {
                Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurity),
                ExpirationDate = expiration,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Succeeded = true
            };
        }
    }
}
