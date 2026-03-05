using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlaySpace.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class agentsmanagement2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessProfileAgents_BusinessProfileId_AgentUserId",
                table: "BusinessProfileAgents");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfileAgents_BusinessProfileId_AgentUserId",
                table: "BusinessProfileAgents",
                columns: new[] { "BusinessProfileId", "AgentUserId" },
                unique: true,
                filter: "\"IsActive\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusinessProfileAgents_BusinessProfileId_AgentUserId",
                table: "BusinessProfileAgents");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfileAgents_BusinessProfileId_AgentUserId",
                table: "BusinessProfileAgents",
                columns: new[] { "BusinessProfileId", "AgentUserId" },
                unique: true,
                filter: "IsActive = 1");
        }
    }
}
