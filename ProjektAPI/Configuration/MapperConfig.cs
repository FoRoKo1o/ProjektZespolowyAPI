﻿using AutoMapper;

namespace ProjektAPI.Configuration
{
    public class MapperConfig : Profile
    {
        public MapperConfig()
        {
            CreateMap<Models.User, Dtos.UserDto>().ReverseMap();
        }
    }
}