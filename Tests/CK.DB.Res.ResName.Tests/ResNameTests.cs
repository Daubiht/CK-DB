﻿using CK.Core;
using CK.SqlServer;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DB.Res.ResName.Tests
{
    [TestFixture]
    public class ResNameTests
    {
        [Test]
        public void resource_0_and_1_are_empty_and_System()
        {
            var r = TestHelper.StObjMap.Default.Obtain<ResNameTable>();
            r.Database.AssertScalarEquals( "", "select ResName from CK.vRes where ResId = 0" );
            r.Database.AssertScalarEquals( "System", "select ResName from CK.vRes where ResId = 1" );
        }


        [Test]
        public void resource_0_and_1_can_not_be_destroyed()
        {
            var p = TestHelper.StObjMap.Default.Obtain<Package>();
            using( var ctx = new SqlStandardCallContext() )
            {
                Assert.Throws<SqlDetailedException>( () => p.ResTable.Destroy( ctx, 0 ) );
                Assert.Throws<SqlDetailedException>( () => p.ResTable.Destroy( ctx, 1 ) );
            }
        }

        [Test]
        public void CreateResName_raises_an_exception_if_the_resource_is_already_associated_to_a_name_or_the_name_already_exists()
        {
            var p = TestHelper.StObjMap.Default.Obtain<Package>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int resId = p.ResTable.Create( ctx );
                string resName = Guid.NewGuid().ToString();
                string resName2 = Guid.NewGuid().ToString();
                p.ResNameTable.CreateResName( ctx, resId, resName );
                // Creates where a name already exists.
                Assert.Throws<SqlDetailedException>( () => p.ResNameTable.CreateResName( ctx, resId, resName2 ) );
                // Creates with an already existing name.
                int resId2 = p.ResTable.Create( ctx );
                Assert.Throws<SqlDetailedException>( () => p.ResNameTable.CreateResName( ctx, resId2, resName ) );
                p.ResTable.Destroy( ctx, resId );
                p.ResTable.Destroy( ctx, resId2 );
            }
        }

        [Test]
        public void renaming_a_resource_can_be_done_WithChildren_or_only_for_the_resource_itself()
        {
            var p = TestHelper.StObjMap.Default.Obtain<Package>();
            using( var ctx = new SqlStandardCallContext() )
            {
                p.ResNameTable.DestroyByResNamePrefix( ctx, "Test", resNameOnly:false );

                int n1 = p.ResNameTable.CreateWithResName( ctx, "Test.Root" );
                int n2 = p.ResNameTable.CreateWithResName( ctx, "Test.Root.1" );
                int n3 = p.ResNameTable.CreateWithResName( ctx, "Test.Root.1.1" );

                p.ResNameTable.Rename( ctx, n1, "Test.-Root-" );
                p.Database.AssertEmptyReader( "select * from CK.tResName where ResName like 'Test.Root%'" )
                          .AssertScalarEquals( n1, "select ResId from CK.tResName where ResName='Test.-Root-'" )
                          .AssertScalarEquals( n2, "select ResId from CK.tResName where ResName='Test.-Root-.1'" )
                          .AssertScalarEquals( n3, "select ResId from CK.tResName where ResName='Test.-Root-.1.1'" );

                p.ResNameTable.Rename( ctx, n1, "Test.MovedTheRootOnly", false );
                p.Database.AssertScalarEquals( n1, "select ResId from CK.tResName where ResName='Test.MovedTheRootOnly'" )
                          .AssertScalarEquals( n2, "select ResId from CK.tResName where ResName='Test.-Root-.1'" )
                          .AssertScalarEquals( n3, "select ResId from CK.tResName where ResName='Test.-Root-.1.1'" );
            }
        }

        [Test]
        public void using_DestroyByPrefix_enables_destruction_without_an_existing_parent()
        {
            var p = TestHelper.StObjMap.Default.Obtain<Package>();
            using( var ctx = new SqlStandardCallContext() )
            {
                var nameRoot = Guid.NewGuid().ToString();

                int n1 = p.ResNameTable.CreateWithResName( ctx, nameRoot + ".Test.Root" );
                int n2 = p.ResNameTable.CreateWithResName( ctx, nameRoot + ".Test.Root.1" );
                int n3 = p.ResNameTable.CreateWithResName( ctx, nameRoot + ".Test.Root.1.1" );
                p.Database.AssertScalarEquals( 3, "select count(*) from CK.tResName where ResName like @0+'%'", nameRoot );

                p.ResNameTable.DestroyByResNamePrefix( ctx, nameRoot, resNameOnly: false );
                p.Database.AssertEmptyReader( "select * from CK.tResName where ResName like @0 + '%'", nameRoot );
            }
        }
    }
}
