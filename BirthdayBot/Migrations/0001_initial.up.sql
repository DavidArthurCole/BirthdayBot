create table guild_settings (
  guild_id bigint primary key,
  feed_channel_id bigint null,
  updated_at timestamptz not null
);

create table birthdays (
  guild_id bigint not null,
  user_id bigint not null,
  month smallint null,
  day smallint null,
  year smallint null,
  time_zone text not null,
  updated_at timestamptz not null,
  primary key (guild_id, user_id),
  check ((month is null) = (day is null))
);

create index birthdays_month_day on birthdays (month, day);

create table announcements (
  guild_id bigint not null,
  user_id bigint not null,
  local_date date not null,
  posted_at timestamptz not null,
  primary key (guild_id, user_id, local_date)
);
