﻿using System.Collections.Generic;
using Abp.Application.Services.Dto;

namespace TestProject.DTO.DeviceTypeDtos
{
    public class DeviceTypeNestedDto : EntityDto
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public int? ParentId { get; set; }

        public List<DeviceTypeNestedDto> Items { get; set; } = new List<DeviceTypeNestedDto>();
    }
}