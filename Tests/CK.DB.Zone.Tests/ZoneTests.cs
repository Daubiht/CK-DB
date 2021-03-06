﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core;
using CK.DB.Actor;
using CK.SqlServer;
using NUnit.Framework;
using CK.SqlServer.Setup;

namespace CK.DB.Zone.Tests
{
    [TestFixture]
    public class ZoneTests
    {
        [TearDown]
        public void CheckInvariants()
        {
            TestHelper.StObjMap.Default.Obtain<SqlDefaultDatabase>().AssertCKCoreInvariants();
        }

        [Test]
        public void zone_0_and_1_can_not_be_destroyed()
        {
            var t = TestHelper.StObjMap.Default.Obtain<ZoneTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                Assert.Throws<SqlDetailedException>( () => t.DestroyZone( ctx, 1, 0 ) );
                Assert.Throws<SqlDetailedException>( () => t.DestroyZone( ctx, 1, 1 ) );
            }
        }

        [Test]
        public void zone_can_be_created_and_destroyed_by_System()
        {
            var t = TestHelper.StObjMap.Default.Obtain<ZoneTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int zoneId = t.CreateZone( ctx, 1 );
                Assert.That( zoneId, Is.GreaterThan( 1 ) );
                t.DestroyZone( ctx, 1, zoneId );
                t.Database.AssertEmptyReader( "select * from CK.tZone where ZoneId=@0", zoneId );
            }
        }

        [Test]
        public void zone_with_existing_groups_can_be_destroyed_when_ForceDestroy_is_true()
        {
            var t = TestHelper.StObjMap.Default.Obtain<ZoneTable>();
            var g = TestHelper.StObjMap.Default.Obtain<GroupTable>();
            var u = TestHelper.StObjMap.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int zoneId = t.CreateZone( ctx, 1 );
                Assert.That( zoneId, Is.GreaterThan( 1 ) );

                int groupId1 = g.CreateGroup( ctx, zoneId );
                int groupId2 = g.CreateGroup( ctx, zoneId );

                int userId = u.CreateUser( ctx, 1, Guid.NewGuid().ToString( "N" ) );
                t.AddUser( ctx, 1, zoneId, userId );
                g.AddUser( ctx, 1, groupId1, userId );
                g.AddUser( ctx, 1, groupId2, userId );

                t.DestroyZone( ctx, 1, zoneId, true );

                t.Database.AssertEmptyReader( "select * from CK.tGroup where ZoneId=@0", zoneId );
                t.Database.AssertEmptyReader( "select * from CK.tZone where ZoneId=@0", zoneId );
            }
        }

        [Test]
        public void Anonymous_cant_not_create_or_destroy_a_zone()
        {
            var p = TestHelper.StObjMap.Default.Obtain<Zone.Package>();
            using( var ctx = new SqlStandardCallContext() )
            {
                Assert.Throws<SqlDetailedException>( () => p.ZoneTable.CreateZone( ctx, 0 ) );

                int zoneId = p.ZoneTable.CreateZone( ctx, 1 );
                Assert.Throws<SqlDetailedException>( () => p.ZoneTable.DestroyZone( ctx, 0, zoneId ) );
                p.ZoneTable.DestroyZone( ctx, 1, zoneId );
            }
        }

        [Test]
        public void adding_a_user_to_a_group_when_he_is_not_registered_in_the_zone_is_an_error()
        {
            var p = TestHelper.StObjMap.Default.Obtain<Zone.Package>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int zoneId = p.ZoneTable.CreateZone( ctx, 1 );
                int userId = p.UserTable.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
                int groupId = p.GroupTable.CreateGroup( ctx, 1, zoneId );

                Assert.Throws<SqlDetailedException>( () => p.GroupTable.AddUser( ctx, 1, groupId, userId ) );

                Assert.DoesNotThrow( () => p.ZoneTable.AddUser( ctx, 1, zoneId, userId ), "Adding the user to the zone." );
                Assert.DoesNotThrow( () => p.GroupTable.AddUser( ctx, 1, groupId, userId ), "Adding the user to group: now it works." );

                Assert.DoesNotThrow( () => p.GroupTable.AddUser( ctx, 1, groupId, userId ), "If the user already exists in the zone, it is okay." );
                Assert.DoesNotThrow( () => p.ZoneTable.AddUser( ctx, 1, zoneId, userId ), "Just like Groups: adding an already existing user to a Zone is okay." );

                p.ZoneTable.DestroyZone( ctx, 1, zoneId, true );
            }
        }

        [Test]
        public void by_default_destroying_a_zone_that_has_a_group_is_an_error_ie_when_ForceDestroy_is_false()
        {
            var p = TestHelper.StObjMap.Default.Obtain<Zone.Package>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int zoneId = p.ZoneTable.CreateZone( ctx, 1 );
                int groupId = p.GroupTable.CreateGroup( ctx, 1, zoneId );

                Assert.Throws<SqlDetailedException>( () => p.ZoneTable.DestroyZone( ctx, 1, zoneId ) );

                p.GroupTable.DestroyGroup( ctx, 1, groupId );
                p.ZoneTable.DestroyZone( ctx, 1, zoneId );

                p.Database.AssertEmptyReader( "select * from CK.tZone where ZoneId=@0", zoneId );
            }
        }

        [Test]
        public void removing_a_user_from_a_Zone_removes_him_from_all_groups()
        {
            var p = TestHelper.StObjMap.Default.Obtain<Zone.Package>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int zoneId = p.ZoneTable.CreateZone( ctx, 1 );
                int userId = p.UserTable.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
                int groupId1 = p.GroupTable.CreateGroup( ctx, 1, zoneId );
                int groupId2 = p.GroupTable.CreateGroup( ctx, 1, zoneId );

                p.ZoneTable.AddUser( ctx, 1, zoneId, userId );
                p.GroupTable.AddUser( ctx, 1, groupId1, userId );
                p.GroupTable.AddUser( ctx, 1, groupId2, userId );

                p.Database.AssertScalarEquals( 3, "select GroupCount = count(*)-1 from CK.tActorProfile where ActorId = @0", userId );

                p.ZoneTable.RemoveUser( ctx, 1, zoneId, userId );

                p.Database.AssertScalarEquals( 0, "select GroupCount = count(*)-1 from CK.tActorProfile where ActorId = @0", userId );

                p.GroupTable.DestroyGroup( ctx, 1, groupId1 );
                p.GroupTable.DestroyGroup( ctx, 1, groupId2 );
                p.ZoneTable.DestroyZone( ctx, 1, zoneId );
                p.UserTable.DestroyUser( ctx, 1, userId );
            }
        }

        [Test]
        public void can_not_create_a_group_in_System_group()
        {
            var map = TestHelper.StObjMap;
            var g = map.Default.Obtain<GroupTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                Assert.Throws<SqlDetailedException>( () => g.CreateGroup( ctx, 1, 1 ) );
            }
        }

        [Test]
        public void groups_can_be_moved_from_its_zone_to_another_one()
        {
            var map = TestHelper.StObjMap;
            var g = map.Default.Obtain<GroupTable>();
            var z = map.Default.Obtain<ZoneTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int idGroup = g.CreateGroup( ctx, 1 );
                int idZone1 = z.CreateZone( ctx, 1 );
                int idZone2 = z.CreateZone( ctx, 1 );

                g.Database.AssertScalarEquals( 0, "select ZoneId from CK.vGroup where GroupId=@0", idGroup );

                g.MoveGroup( ctx, 1, idGroup, idZone1 );
                g.Database.AssertScalarEquals( idZone1, "select ZoneId from CK.vGroup where GroupId=@0", idGroup );

                g.MoveGroup( ctx, 1, idGroup, idZone2 );
                g.Database.AssertScalarEquals( idZone2, "select ZoneId from CK.vGroup where GroupId=@0", idGroup );

                g.MoveGroup( ctx, 1, idGroup, 0 );
                g.Database.AssertScalarEquals( 0, "select ZoneId from CK.vGroup where GroupId=@0", idGroup );

                g.DestroyGroup( ctx, 1, idGroup );
                z.DestroyZone( ctx, 1, idZone1 );
                z.DestroyZone( ctx, 1, idZone2 );
            }
        }

        [Test]
        public void by_default_when_a_group_is_moved_all_of_its_users_must_be_already_registered_in_the_target_zone()
        {
            var map = TestHelper.StObjMap;
            var g = map.Default.Obtain<GroupTable>();
            var z = map.Default.Obtain<ZoneTable>();
            var u = map.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int idUser = u.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
                int idGroup = g.CreateGroup( ctx, 1 );
                int idZoneEmpty = z.CreateZone( ctx, 1 );
                int idZoneOK = z.CreateZone( ctx, 1 );

                g.AddUser( ctx, 1, idGroup, idUser );
                z.AddUser( ctx, 1, idZoneOK, idUser );
                // This works since the user is in the zoneOK.
                g.MoveGroup( ctx, 1, idGroup, idZoneOK );
                // User is in the Group and in the ZoneOK.
                u.Database.AssertScalarEquals( idUser, $"select ActorId from CK.tActorProfile where GroupId = {idGroup} and ActorId = {idUser}" );
                u.Database.AssertScalarEquals( idUser, $"select ActorId from CK.tActorProfile where GroupId = {idZoneOK} and ActorId = {idUser}" );

                // This does not: ZoneEmpty does not contain the user.
                // This uses the default option: GroupMoveOption.None.
                Assert.Throws<SqlDetailedException>( () => g.MoveGroup( ctx, 1, idGroup, idZoneEmpty ) );
                // User is still in the Group.
                u.Database.AssertScalarEquals( idUser, $"select ActorId from CK.tActorProfile where GroupId = {idGroup} and ActorId = {idUser}" );
                // ...and still not in the ZoneEmpty.
                u.Database.AssertEmptyReader( $"select ActorId from CK.tActorProfile where GroupId = {idZoneEmpty} and ActorId = {idUser}" );
            }
        }

        [Test]
        public void with_option_Intersect_when_a_group_is_moved_its_users_not_already_registered_in_the_target_zone_are_removed()
        {
            var map = TestHelper.StObjMap;
            var g = map.Default.Obtain<GroupTable>();
            var z = map.Default.Obtain<ZoneTable>();
            var u = map.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int idUser = u.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
                int idGroup = g.CreateGroup( ctx, 1 );
                int idZoneEmpty = z.CreateZone( ctx, 1 );
                int idZoneOK = z.CreateZone( ctx, 1 );

                g.AddUser( ctx, 1, idGroup, idUser );
                z.AddUser( ctx, 1, idZoneOK, idUser );
                // This works since the user is in the zoneOK (Intersect does nothing).
                g.MoveGroup( ctx, 1, idGroup, idZoneOK, GroupMoveOption.Intersect );
                // User is in the Group and in the ZoneOK.
                u.Database.AssertScalarEquals( idUser, $"select ActorId from CK.tActorProfile where GroupId = {idGroup} and ActorId = {idUser}" );
                u.Database.AssertScalarEquals( idUser, $"select ActorId from CK.tActorProfile where GroupId = {idZoneOK} and ActorId = {idUser}" );

                // This does work... But the user is removed from the group
                // to preserve the 'Group.UserNotInZone' invariant.
                g.MoveGroup( ctx, 1, idGroup, idZoneEmpty, GroupMoveOption.Intersect );
                // User is no more in the Group: it has been removed.
                u.Database.AssertEmptyReader( $"select ActorId from CK.tActorProfile where GroupId = {idGroup} and ActorId = {idUser}" );
                // ...and still not in the ZoneEmpty.
                u.Database.AssertEmptyReader( $"select ActorId from CK.tActorProfile where GroupId = {idZoneEmpty} and ActorId = {idUser}" );
            }
        }

        [Test]
        public void with_option_AutoUserRegistration_when_a_group_is_moved_its_users_not_already_registered_in_the_target_zone_are_automatically_registered()
        {
            var map = TestHelper.StObjMap;
            var g = map.Default.Obtain<GroupTable>();
            var z = map.Default.Obtain<ZoneTable>();
            var u = map.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int idUser = u.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
                int idGroup = g.CreateGroup( ctx, 1 );
                int idZoneEmpty = z.CreateZone( ctx, 1 );
                int idZoneOK = z.CreateZone( ctx, 1 );

                g.AddUser( ctx, 1, idGroup, idUser );
                z.AddUser( ctx, 1, idZoneOK, idUser );
                // This works since the user is in the zoneOK (Intersect does nothing).
                g.MoveGroup( ctx, 1, idGroup, idZoneOK, GroupMoveOption.AutoUserRegistration );
                // User is in the Group and in the ZoneOK.
                u.Database.AssertScalarEquals( idUser, $"select ActorId from CK.tActorProfile where GroupId = {idGroup} and ActorId = {idUser}" );
                u.Database.AssertScalarEquals( idUser, $"select ActorId from CK.tActorProfile where GroupId = {idZoneOK} and ActorId = {idUser}" );

                // This does work: and the user is automatically added to the target Zone!
                // (the 'Group.UserNotInZone' invariant is preserved).
                g.MoveGroup( ctx, 1, idGroup, idZoneEmpty, GroupMoveOption.AutoUserRegistration );
                // User is no more in the Group: it has been removed.
                u.Database.AssertScalarEquals( idUser, $"select ActorId from CK.tActorProfile where GroupId = {idGroup} and ActorId = {idUser}" );
                // ...and still not in the ZoneEmpty.
                u.Database.AssertScalarEquals( idUser, $"select ActorId from CK.tActorProfile where GroupId = {idZoneEmpty} and ActorId = {idUser}" );
            }
        }

    }
}
