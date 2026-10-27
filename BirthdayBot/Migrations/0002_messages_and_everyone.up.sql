alter table guild_settings add column ping_everyone boolean not null default true;

create table birthday_messages (
  guild_id bigint not null,
  user_id bigint not null,
  message text not null,
  updated_by bigint not null,
  updated_at timestamptz not null,
  primary key (guild_id, user_id)
);
