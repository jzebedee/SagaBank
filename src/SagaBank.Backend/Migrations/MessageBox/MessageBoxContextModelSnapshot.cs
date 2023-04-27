﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SagaBank.Shared.Contexts;

#nullable disable

namespace SagaBank.Backend.Migrations.MessageBox
{
    [DbContext(typeof(MessageBoxContext))]
    partial class MessageBoxContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.5");

            modelBuilder.Entity("SagaBank.Shared.Contexts.InboxMessage", b =>
                {
                    b.Property<byte[]>("Id")
                        .HasColumnType("BLOB");

                    b.Property<string>("Type")
                        .HasColumnType("TEXT");

                    b.Property<DateTimeOffset>("Added")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasDefaultValueSql("datetime('now')");

                    b.HasKey("Id", "Type");

                    b.ToTable("Inbox");
                });

            modelBuilder.Entity("SagaBank.Shared.Contexts.OutboxMessage", b =>
                {
                    b.Property<byte[]>("Id")
                        .HasColumnType("BLOB");

                    b.Property<string>("Type")
                        .HasColumnType("TEXT");

                    b.Property<DateTimeOffset>("Added")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasDefaultValueSql("datetime('now')");

                    b.Property<byte[]>("Payload")
                        .IsRequired()
                        .HasColumnType("BLOB");

                    b.Property<int>("State")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasDefaultValue(0);

                    b.HasKey("Id", "Type");

                    b.ToTable("Outbox");
                });
#pragma warning restore 612, 618
        }
    }
}
