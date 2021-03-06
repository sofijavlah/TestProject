﻿using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Abp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using TestProject.DTO.DeviceTypeDtos;
using TestProject.Models;
using TestProject.Services.DeviceServices;

namespace TestProject.Services.DeviceTypeServices
{
    public class DeviceTypeService : TestProjectAppServiceBase, IDeviceTypeService
    {
        private readonly IRepository<DeviceType> _deviceTypeRepository;
        private readonly IRepository<DeviceTypeProperty> _propertyRepository;
        private readonly IRepository<Device> _deviceRepository;
        private readonly IRepository<DevicePropertyValue> _valueRepository;


        /// <summary>
        ///     Initializes a new instance of the <see cref="DeviceService" /> class.
        /// </summary>
        /// <param name="deviceRepository">The device repository.</param>
        /// <param name="deviceTypeRepository">The device type repository.</param>
        /// <param name="propertyRepository">The property repository.</param>
        /// <param name="valueRepository">The value repository.</param>
        public DeviceTypeService(IRepository<DeviceType> deviceTypeRepository,
            IRepository<DeviceTypeProperty> propertyRepository, IRepository<Device> deviceRepository, IRepository<DevicePropertyValue> valueRepository)
        {
            _deviceTypeRepository = deviceTypeRepository;
            _propertyRepository = propertyRepository;
            _deviceRepository = deviceRepository;
            _valueRepository = valueRepository;
        }

        
        // ------------------------------ GET NESTED LIST OF TYPES -------------------------------//

        public List<DeviceTypeNestedDto> GetDeviceTypes(int? parentId)
        {
            var baseDeviceTypes = _deviceTypeRepository.GetAll()
                .Where(x => x.ParentDeviceTypeId == parentId).ToList();

            var result = new List<DeviceTypeNestedDto>();

            foreach (var deviceType in baseDeviceTypes)
            {
                var currentType = new DeviceTypeNestedDto
                {
                    Id = deviceType.Id,
                    Name = deviceType.Name,
                    Description = deviceType.Description,
                    ParentId = deviceType.ParentDeviceTypeId,
                    Items = GetDeviceTypes(deviceType.Id)
                };

                result.Add(currentType);
            }

            return result;
        }



        //------------------------GET FLAT LIST OF TYPES WITH PROPERTIES -------------------------//
        //----------- returns flat list of types, containing type with given id and parents------//

        public IEnumerable<DeviceTypePropertiesDto> GetDeviceTypesWithProperties(int? id)
        {
            var result = new List<DeviceTypePropertiesDto>();
            
            var type = _deviceTypeRepository.GetAll().Include(x => x.DeviceTypeProperties).Include(x => x.Devices).ThenInclude(x => x.DevicePropertyValues)
                .First(x => x.Id == id);
            
            var currentType = new DeviceTypePropertiesDto
            {
                Id = type.Id,
                Name = type.Name,
                Description = type.Description,
                ParentId = type.ParentDeviceTypeId,
                Properties = ObjectMapper.Map<List<DeviceTypePropertyDto>>(type.DeviceTypeProperties)
            };

            if (_deviceTypeRepository.GetAll().Count() == 1)
            {
                result.Add(currentType);
                return result;
            }

            if (type.ParentDeviceTypeId == null)
            {
                result.Add(currentType);
                return result;
            }

            result.Add(currentType);

            return result.Concat(GetDeviceTypesWithProperties(type.ParentDeviceTypeId)).OrderBy(x => x.Id);
        }



        // ---------------GET FLAT LIST OF TYPES STARTING WITH THE PARENT -----------------//

        public IEnumerable<DeviceType> GetDeviceTypeWithChildren(int parentId)
        {
            var type = _deviceTypeRepository.GetAll().Include(x => x.Devices).ThenInclude(x => x.DevicePropertyValues)
                .Include(x => x.DeviceTypeProperties)
                .First(x => x.Id == parentId);

            var children = _deviceTypeRepository.GetAll().Include(x => x.Devices).ThenInclude(x => x.DevicePropertyValues)
                .Include(x => x.DeviceTypeProperties)
                .Where(x => x.ParentDeviceTypeId == parentId).ToList();

            var list = new List<DeviceType>();

            if (!children.Any())
            {
                list.Add(type);
                return list;
            }

            foreach (var child in children)
            {
                list.AddRange(GetDeviceTypeWithChildren(child.Id));
            }

            list.Add(type);
            return list;
        }




        // ---------------GET FLAT LIST OF TYPES STARTING WITH THE CHILD -----------------//

        public IEnumerable<DeviceType> GetDeviceTypeWithParents(int? id)
        {
            var type = _deviceTypeRepository.GetAll().Include(x => x.Devices).ThenInclude(x => x.DevicePropertyValues)
                .Include(x => x.DeviceTypeProperties)
                .First(x => x.Id == id);
            
            var list = new List<DeviceType>();

            if (type.ParentDeviceTypeId == null)
            {
                list.Add(type);
                return list;
            }

            list.Add(type);
            return list.Concat(GetDeviceTypeWithParents(type.ParentDeviceTypeId)).OrderBy(x => x.Id);
        }



        //----------------- DYNAMIC DEVICE DETAILS CONTAINING PROPERTIES ------------------//

        public List<IDictionary<string, object>> GetDevicesByType(int? id)
        {
            var deviceTypes = GetDeviceTypeWithParents(id);

            List<IDictionary<string, object>> result = new List<IDictionary<string, object>>();

            var allProperties = new List<DeviceTypeProperty>();

            var types = deviceTypes as DeviceType[] ?? deviceTypes.ToArray();
            foreach (var type in types)
            {
                allProperties.AddRange(type.DeviceTypeProperties);
            }

            foreach (var type in types)
            {
                foreach (var device in type.Devices)
                {
                    var values = device.DevicePropertyValues;

                    IDictionary<string, object> expando = new ExpandoObject();

                    expando.Add("Id", device.Id);
                    expando.Add("Name", device.Name);
                    expando.Add("Description", device.Description);

                    foreach (var prop in allProperties)
                    {
                        if (!device.DevicePropertyValues.Any())
                        {
                            string propName = prop.DeviceType.Name + "-" + prop.Name;

                            expando.Add(propName, null);
                        }

                        foreach (var value in values)
                        {

                            if (value.DeviceTypePropertyId == prop.Id)
                            {
                                string propName = prop.DeviceType.Name + "-" + prop.Name;

                                expando.Add(propName, value.Value);
                            }
                        }
                    }

                    result.Add(expando);
                }
            }

            return result;
        }
        
        
        //---------------------- CREATE NEW TYPE -----------------------//
        public void CreateOrUpdateDeviceType(DeviceTypePropertiesDto input)
        {
            if (input.Id == 0)
            {
                var newDeviceType = new DeviceType
                {
                    Name = input.Name,
                    Description = input.Description,
                    ParentDeviceTypeId = input.ParentId
                };

                var newDeviceTypeId = _deviceTypeRepository.InsertAndGetId(newDeviceType);

                foreach (var property in input.Properties)
                {
                    _propertyRepository.Insert(new DeviceTypeProperty
                    {
                        Name = property.NameProperty,
                        IsRequired = property.Required,
                        Type = property.Type,
                        DeviceTypeId = newDeviceTypeId
                    });
                }

                return;
            }

            var targetType = _deviceTypeRepository.GetAll().Include(x => x.DeviceTypeProperties)
                .First(x => x.Id == input.Id);

            targetType.Name = input.Name;
            targetType.Description = input.Description;
            //add option to change parent;
            targetType.ParentDeviceTypeId = input.ParentId;

            var updatedProperties = input.Properties.Where(x => x.DeviceTypeId == targetType.Id).ToList();

            foreach (var prop in targetType.DeviceTypeProperties)
            {
                var pr = updatedProperties.FirstOrDefault(x => x.Id == prop.Id);

                if (pr == null)
                {
                    _propertyRepository.Delete(prop);
                }
            }

            foreach (var property in updatedProperties)
            {
                if (property.Id == 0)
                {
                    _propertyRepository.Insert(new DeviceTypeProperty
                    {
                        Name = property.NameProperty,
                        IsRequired = property.Required,
                        Type = property.Type,
                        DeviceTypeId = input.Id
                    });

                    continue;
                }

                var prop = _propertyRepository.Get(property.Id);
                ObjectMapper.Map(prop, property);
            }
        }


        // --------------------------- DELETE TYPE ------------------------------//

        public void DeleteDeviceType(int id)
        {
            var types = GetDeviceTypeWithChildren(id).ToList();

            var orderedTypes = types.OrderByDescending(x => x.Id);

            foreach (var type in orderedTypes)
            {
                var devices = type.Devices;

                foreach (var device in devices)
                {
                    var values = device.DevicePropertyValues;

                    foreach (var value in values)
                    {
                        _valueRepository.Delete(value);
                    }

                    _deviceRepository.Delete(device);
                }

                _deviceTypeRepository.Delete(type);
            }
        }
        
    }
}