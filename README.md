# Dapper ClassMate

Dapper ClassMate is a Visual Studio extension that generates Dapper boilerplate from SQL Server objects.

It helps you quickly create:
- Request models for stored procedure inputs
- Result POCOs for result sets and output parameters
- Async repository methods with Dapper

## Why Use It

Writing repetitive data-access code is slow and error-prone. Dapper ClassMate scaffolds the common pieces so you can focus on business logic.

## Features

- Works with SQL Server stored procedures, tables, and views
- Generates strongly typed request and result classes
- Generates async repository methods with `CancellationToken`
- Supports command timeout configuration
- Includes preview and copy workflow inside Visual Studio

## Requirements

- Visual Studio 2022
- .NET Framework 4.8.1 / .NET Standard 2.0 solution support
- SQL Server connection access

## Quick Start

1. Open the command in Visual Studio.
2. Enter a connection string and load database objects.
3. Select a stored procedure, table, or view.
4. Click **Generate**.
5. Copy the generated code into your project.

## Publisher

George Hendrickson