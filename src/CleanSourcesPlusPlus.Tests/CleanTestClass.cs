// NUnit 3 tests
// See documentation : https://github.com/nunit/docs/wiki/NUnit-Documentation
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using CleanSourcesPlusPlus;

namespace CleanSourcesPlusPlus.Tests
{
    [TestFixture]
    public class CleanTestClass
    {
        [Test]
        public void TestMethod()
        {
            Clean.ZipDirectory(@"C:\projects\wood\repos\source\Bitbucket\edms-bad-durkheim.bak");
        }
    }
}
