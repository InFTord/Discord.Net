﻿using Discord.API.Rest;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Model = Discord.API.GuildMember;

namespace Discord.Rest
{
    [DebuggerDisplay(@"{DebuggerDisplay,nq}")]
    public class RestGuildUser : RestUser, IGuildUser, IUpdateable
    {
        private long? _joinedAtTicks;
        private ImmutableArray<ulong> _roleIds;

        public string Nickname { get; private set; }
        internal IGuild Guild { get; private set; }
        public bool IsDeafened { get; private set; }
        public bool IsMuted { get; private set; }

        public ulong GuildId => Guild.Id;
        public GuildPermissions GuildPermissions
        {
            get
            {
                if (!Guild.Available)
                    throw new InvalidOperationException("Resolving permissions requires the parent guild to be downloaded.");
                return new GuildPermissions(Permissions.ResolveGuild(Guild, this));
            }
        }
        public IReadOnlyCollection<ulong> RoleIds => _roleIds;
        
        public DateTimeOffset? JoinedAt => DateTimeUtils.FromTicks(_joinedAtTicks);

        internal RestGuildUser(BaseDiscordClient discord, IGuild guild, ulong id)
            : base(discord, id)
        {
            Guild = guild;
        }
        internal static RestGuildUser Create(BaseDiscordClient discord, IGuild guild, Model model)
        {
            var entity = new RestGuildUser(discord, guild, model.User.Id);
            entity.Update(model);
            return entity;
        }
        internal void Update(Model model)
        {
            _joinedAtTicks = model.JoinedAt.UtcTicks;
            if (model.Nick.IsSpecified)
                Nickname = model.Nick.Value;
            IsDeafened = model.Deaf;
            IsMuted = model.Mute;
            UpdateRoles(model.Roles);
        }
        private void UpdateRoles(ulong[] roleIds)
        {
            var roles = ImmutableArray.CreateBuilder<ulong>(roleIds.Length + 1);
            roles.Add(Guild.Id);
            for (int i = 0; i < roleIds.Length; i++)
                roles.Add(roleIds[i]);
            _roleIds = roles.ToImmutable();
        }
        
        public override async Task UpdateAsync(RequestOptions options = null)
        {
            var model = await Discord.ApiClient.GetGuildMemberAsync(GuildId, Id, options);
            Update(model);
        }
        public async Task ModifyAsync(Action<ModifyGuildMemberParams> func, RequestOptions options = null)
        { 
            var args = await UserHelper.ModifyAsync(this, Discord, func, options);
            if (args.Deaf.IsSpecified)
                IsDeafened = args.Deaf.Value;
            if (args.Mute.IsSpecified)
                IsMuted = args.Mute.Value;
            if (args.RoleIds.IsSpecified)
                UpdateRoles(args.RoleIds.Value);
        }
        public Task KickAsync(RequestOptions options = null)
            => UserHelper.KickAsync(this, Discord, options);

        public ChannelPermissions GetPermissions(IGuildChannel channel)
        {
            var guildPerms = GuildPermissions;
            return new ChannelPermissions(Permissions.ResolveChannel(Guild, this, channel, guildPerms.RawValue));
        }

        //IVoiceState
        bool IVoiceState.IsSelfDeafened => false;
        bool IVoiceState.IsSelfMuted => false;
        bool IVoiceState.IsSuppressed => false;
        IVoiceChannel IVoiceState.VoiceChannel => null;
        string IVoiceState.VoiceSessionId => null;
    }
}
