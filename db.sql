CREATE TABLE people(
	id uuid not null primary key,
	nickname varchar(32) not null unique,
	name varchar(100) not null,
	birth_date char(10) not null,
	stack varchar(32)[]
);
