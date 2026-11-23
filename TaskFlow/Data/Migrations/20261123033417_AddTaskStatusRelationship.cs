
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Data.Migrations
{
    public partial class AddTaskStatusRelationship : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Tasks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "TaskStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(
                        type: "int",
                        nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),

                    Name = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: false),

                    Description = table.Column<string>(
                        type: "nvarchar(max)",
                        nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskStatuses", x => x.Id);
                });

            // Insert the default statuses.
            migrationBuilder.InsertData(
                table: "TaskStatuses",
                columns: new[] { "Id", "Name", "Description" },
                values: new object[,]
                {
                    { 1, "To Do", null },
                    { 2, "In Progress", null },
                    { 3, "Done", null }
                });

            // Add the FK temporarily as nullable.
            migrationBuilder.AddColumn<int>(
                name: "TaskStatusId",
                table: "Tasks",
                type: "int",
                nullable: true);

            // Preserve existing task statuses.
            migrationBuilder.Sql(@"
                UPDATE Tasks
                SET TaskStatusId = CASE
                    WHEN Status = 'In Progress' THEN 2
                    WHEN Status = 'Done' THEN 3
                    ELSE 1
                END;
            ");

            // Make the FK required after assigning every task.
            migrationBuilder.AlterColumn<int>(
                name: "TaskStatusId",
                table: "Tasks",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TaskStatusId",
                table: "Tasks",
                column: "TaskStatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_TaskStatuses_TaskStatusId",
                table: "Tasks",
                column: "TaskStatusId",
                principalTable: "TaskStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Drop the old column only after migrating its data.
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tasks");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the old string column.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            // Restore each task's original status name.
            migrationBuilder.Sql(@"
                UPDATE Tasks
                SET Status = TaskStatuses.Name
                FROM Tasks
                INNER JOIN TaskStatuses
                    ON Tasks.TaskStatusId = TaskStatuses.Id;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_TaskStatuses_TaskStatusId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_TaskStatusId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "TaskStatusId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "TaskStatuses");

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
