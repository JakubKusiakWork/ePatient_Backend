using System.Collections.Generic;
using ePatientApi.Models;

namespace ePatientApi.Tests.Data
{
    public static class TestData
    {
        private static int _nextId = 1;
        public static int NextId() => System.Threading.Interlocked.Increment(ref _nextId);

        public static List<RegisteredPatient> GetPatient()
        {
            return new List<RegisteredPatient>
            {
                new RegisteredPatient
                {
                    Id = NextId(),
                    FirstName = "Test",
                    LastName = "User",
                    Username = "TestUser",
                    Email = "testUser@gmail.com",
                    PhoneNumber = "0918777666",
                    HashedPassword = "userpassword123"
                }
            }; 
        }
    }
}