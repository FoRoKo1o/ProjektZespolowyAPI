﻿using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace ProjektAPI.Services
{
    public class UserService
    {
        private readonly AppDbContext _dbContext;
        private readonly IMapper _mapper;
        public UserService(AppDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }
        public async Task<bool> CheckIfUserExists(string login)
        {
            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Login == login);
            return user != null;
        }
    }
}
