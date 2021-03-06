﻿using CK.Core;
using CK.Text;
using CK.DB.Actor;
using CK.DB.HZone;
using CK.SqlServer;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.SqlServer.Setup;

namespace CK.DB.HZone.Tests
{
    [TestFixture]
    public class HZoneSimpleTests
    {
        [TearDown]
        public void CheckCKCoreInvariant()
        {
            TestHelper.StObjMap.Default.Obtain<SqlDefaultDatabase>().AssertCKCoreInvariants();
        }

        [Test]
        public void adding_a_user_in_a_child_zone_support_AutoAddUserInParentZone()
        {
            var map = TestHelper.StObjMap;
            var zone = map.Default.Obtain<ZoneTable>();
            var group = map.Default.Obtain<Zone.GroupTable>();
            var user = map.Default.Obtain<UserTable>();

            using( var ctx = new SqlStandardCallContext() )
            {
                int idUser1 = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
                int idUser2 = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
                var allZones = new List<int>();
                allZones.Add( zone.CreateZone( ctx, 1 ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[0] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[1] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[2] ) );
                int idGroup = group.CreateGroup( ctx, 1, allZones[3] );

                Assert.Throws<SqlDetailedException>( () => zone.AddUser( ctx, 1, allZones[3], idUser1, autoAddUserInParentZone: false ) );
                zone.AddUser( ctx, 1, allZones[3], idUser1, autoAddUserInParentZone: true );

                Assert.Throws<SqlDetailedException>( () => group.AddUser( ctx, 1, idGroup, idUser2, autoAddUserInZone: false ) );
                group.AddUser( ctx, 1, idGroup, idUser2, autoAddUserInZone: true );

                user.Database.AssertScalarEquals( 4, "select count(*) from CK.tActorProfile where ActorId <> GroupId and ActorId = @0", idUser1 );
                user.Database.AssertScalarEquals( 5, "select count(*) from CK.tActorProfile where ActorId <> GroupId and ActorId = @0", idUser2 );

                zone.DestroyZone( ctx, 1, allZones[0], forceDestroy: true );
            }
        }

        [Test]
        public void removing_a_user_from_a_zone_removes_it_from_all_child_zones()
        {
            var map = TestHelper.StObjMap;
            var zone = map.Default.Obtain<ZoneTable>();
            var group = map.Default.Obtain<Zone.GroupTable>();
            var user = map.Default.Obtain<UserTable>();

            using( var ctx = new SqlStandardCallContext() )
            {
                int idUser1 = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
                int idUser2 = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
                var allZones = new List<int>();
                allZones.Add( zone.CreateZone( ctx, 1 ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[0] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[1] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[2] ) );
                int idGroup = group.CreateGroup( ctx, 1, allZones[3] );

                zone.AddUser( ctx, 1, allZones[3], idUser1, autoAddUserInParentZone: true );
                group.AddUser( ctx, 1, idGroup, idUser2, autoAddUserInZone: true );

                user.Database.AssertScalarEquals( 4, "select count(*) from CK.tActorProfile where ActorId <> GroupId and ActorId = @0", idUser1 );
                user.Database.AssertScalarEquals( 5, "select count(*) from CK.tActorProfile where ActorId <> GroupId and ActorId = @0", idUser2 );

                zone.RemoveUser( ctx, 1, allZones[2], idUser2 );
                user.Database.AssertScalarEquals( 2, "select count(*) from CK.tActorProfile where ActorId <> GroupId and ActorId = @0", idUser2 );
                group.RemoveUser( ctx, 1, allZones[1], idUser2 );
                user.Database.AssertScalarEquals( 1, "select count(*) from CK.tActorProfile where ActorId <> GroupId and ActorId = @0", idUser2 );
                group.RemoveUser( ctx, 1, allZones[0], idUser2 );
                user.Database.AssertScalarEquals( 0, "select count(*) from CK.tActorProfile where ActorId <> GroupId and ActorId = @0", idUser2 );

                zone.RemoveUser( ctx, 1, allZones[0], idUser1 );
                user.Database.AssertScalarEquals( 0, "select count(*) from CK.tActorProfile where ActorId <> GroupId and ActorId = @0", idUser2 );

                zone.DestroyZone( ctx, 1, allZones[0], forceDestroy: true );
            }
        }

        [Test]
        public void creating_and_destroying_zone_with_sub_zones_and_groups_when_ForceDestroy_is_true()
        {
            var map = TestHelper.StObjMap;
            var zone = map.Default.Obtain<ZoneTable>();
            var group = map.Default.Obtain<Zone.GroupTable>();
            var user = map.Default.Obtain<UserTable>();

            using( var ctx = new SqlStandardCallContext() )
            {
                var allZones = new List<int>();
                var allGroups = new List<int>();
                allZones.Add( zone.CreateZone( ctx, 1 ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[0] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[0] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[1] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[1] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[3] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[3] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[4] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[4] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[4] ) );
                allZones.Add( zone.CreateZone( ctx, 1, allZones[4] ) );
                foreach( var idZone in allZones )
                {
                    allGroups.Add( group.CreateGroup( ctx, 1, idZone ) );
                }
                Assert.Throws<SqlDetailedException>( () => zone.DestroyZone( ctx, 1, allZones[0], forceDestroy: false ) );
                zone.DestroyZone( ctx, 1, allZones[0], forceDestroy: true );
            }
        }

        [Test]
        public void moving_a_zone_in_the_tree_can_specify_the_next_sibling_id()
        {
            var map = TestHelper.StObjMap;
            var zone = map.Default.Obtain<ZoneTable>();

            using( var ctx = new SqlStandardCallContext() )
            {
                var z = new List<int>();
                for( int i = 0; i < 10; ++i ) z.Add( zone.CreateZone( ctx, 1 ) );
                zone.MoveZone( ctx, 1, z[1], z[0] );
                zone.MoveZone( ctx, 1, z[2], z[0] );
                zone.MoveZone( ctx, 1, z[3], z[0] );
                zone.MoveZone( ctx, 1, z[4], z[0] );
                zone.CheckTree( ctx, z[0], $@"
                                {z[0]}
                                +{z[1]}
                                +{z[2]}
                                +{z[3]}
                                +{z[4]}
                                " );
                zone.MoveZone( ctx, 1, z[6], z[5] );
                zone.MoveZone( ctx, 1, z[7], z[5], nextSiblingId: z[6] );
                zone.MoveZone( ctx, 1, z[8], z[5] );
                zone.MoveZone( ctx, 1, z[9], z[5], nextSiblingId: z[8] );
                zone.CheckTree( ctx, z[5], $@"
                                {z[5]}
                                +{z[7]}
                                +{z[6]}
                                +{z[9]}
                                +{z[8]}
                                " );
            }
        }

        [Test]
        public void GroupMove_can_safely_be_called_instead_of_ZoneMove()
        {
            var map = TestHelper.StObjMap;
            var zone = map.Default.Obtain<ZoneTable>();
            var group = map.Default.Obtain<Zone.GroupTable>();

            using( var ctx = new SqlStandardCallContext() )
            {
                var z = new List<int>();
                for( int i = 0; i < 5; ++i ) z.Add( zone.CreateZone( ctx, 1 ) );
                group.MoveGroup( ctx, 1, z[1], z[0] );
                group.MoveGroup( ctx, 1, z[4], z[0] );
                group.MoveGroup( ctx, 1, z[2], z[0] );
                group.MoveGroup( ctx, 1, z[3], z[0] );
                zone.CheckTree( ctx, z[0], $@"
                                {z[0]}
                                +{z[4]}
                                +{z[1]}
                                +{z[2]}
                                +{z[3]}
                                " );
                group.MoveGroup( ctx, 1, z[3], z[2] );
                group.MoveGroup( ctx, 1, z[4], z[3] );
                zone.CheckTree( ctx, z[0], $@"
                                {z[0]}
                                +{z[1]}
                                +{z[2]}
                                ++{z[3]}
                                +++{z[4]}
                                " );
            }
        }

        [Test]
        public void moving_a_zone_in_a_child_zone_is_an_error()
        {
            var map = TestHelper.StObjMap;
            var zone = map.Default.Obtain<ZoneTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int idZone1 = zone.CreateZone( ctx, 1 );
                int idZone2 = zone.CreateZone( ctx, 1, idZone1 );

                Assert.Throws<SqlDetailedException>( () => zone.MoveZone( ctx, 1, idZone1, idZone2 ) );
            }
        }
    }
}