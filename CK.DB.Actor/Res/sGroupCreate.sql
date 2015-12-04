-- Version = 1.0.2
--
-- Creates a Group.
--
create procedure CK.sGroupCreate 
(
	@ActorId int,
	@GroupIdResult int output
)
as begin
    if @ActorId <= 0 raiserror( 'Security.AnonymousNotAllowed', 16, 1 );

	--[beginsp]
	
	--<Extension Name="Group.PreCreate" />

	exec CK.sActorCreate @ActorId, @GroupIdResult output;
	insert into CK.tGroup( GroupId ) values( @GroupIdResult );

	--<Extension Name="Group.PostCreate" />

	--[endsp]
end