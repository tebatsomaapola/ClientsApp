
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using ClientsApp.Models;

namespace ClientsApp.Controllers
{
    public class ClientsController : Controller
    {
        private readonly string _conn = "Data Source=clients.db";

        public IActionResult Index()
        {
            var model = new ClientsViewModel();
            using (var connection = new SqliteConnection(_conn))
            {
                connection.Open();
                // Load Clients
                var cmd1 = connection.CreateCommand();
                cmd1.CommandText = "SELECT Name, ClientCode, LinkedContactsCount FROM Clients ORDER BY Name ASC";
                using var r1 = cmd1.ExecuteReader();
                while (r1.Read()) model.Clients.Add(new Client { Name = r1.GetString(0), ClientCode = r1.GetString(1), LinkedContactsCount = r1.GetInt32(2) });

                //  Load Contacts for linking 
                var cmd2 = connection.CreateCommand();
                cmd2.CommandText = "SELECT Id, Name, Surname, ClientCode FROM Contacts";
                using var r2 = cmd2.ExecuteReader();
                while (r2.Read()) model.Contacts.Add(new Contact { Id = r2.GetInt32(0), Name = r2.GetString(1), Surname = r2.GetString(2), ClientCode = r2.IsDBNull(3) ? "" : r2.GetString(3) });
            }
            return View(model);
        }

        // Create client
        [HttpPost]
        public IActionResult CreateClient(Client newClient)
        {
            string code = GenerateClientCode(newClient.Name);
            using var connection = new SqliteConnection(_conn);
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO Clients (Name, ClientCode, LinkedContactsCount) VALUES ($n, $c, 0)";
            cmd.Parameters.AddWithValue("$n", newClient.Name);
            cmd.Parameters.AddWithValue("$c", code);
            cmd.ExecuteNonQuery();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult UpdateLink(int contactId, string newClientCode)
        {
            using var connection = new SqliteConnection(_conn);
            connection.Open();
            using var trans = connection.BeginTransaction();

            var getOld = connection.CreateCommand();
            getOld.CommandText = "SELECT ClientCode FROM Contacts WHERE Id = $id";
            getOld.Parameters.AddWithValue("$id", contactId);
            string oldCode = getOld.ExecuteScalar()?.ToString();

            if (!string.IsNullOrEmpty(oldCode)) {
                var dec = connection.CreateCommand();
                dec.CommandText = "UPDATE Clients SET LinkedContactsCount = MAX(0, LinkedContactsCount - 1) WHERE ClientCode = $c";
                dec.Parameters.AddWithValue("$c", oldCode);
                dec.ExecuteNonQuery();
            }

            var upd = connection.CreateCommand();
            upd.CommandText = "UPDATE Contacts SET ClientCode = $nc WHERE Id = $id";
            upd.Parameters.AddWithValue("$nc", (object)newClientCode ?? System.DBNull.Value);
            upd.Parameters.AddWithValue("$id", contactId);
            upd.ExecuteNonQuery();

            if (!string.IsNullOrEmpty(newClientCode)) {
                var inc = connection.CreateCommand();
                inc.CommandText = "UPDATE Clients SET LinkedContactsCount = LinkedContactsCount + 1 WHERE ClientCode = $c";
                inc.Parameters.AddWithValue("$c", newClientCode);
                inc.ExecuteNonQuery();
            }
            trans.Commit();
            return RedirectToAction("Index");
        }

      // Generate client code whilr checking if exist in DB
        private string GenerateClientCode(string name) {
            string[] words = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            string prefix = words.Length >= 3 ? string.Join("", words.Take(3).Select(w => char.ToUpper(w[0]))) : (name.Replace(" ", "").ToUpper() + "ABC").Substring(0, 3);
            using var conn = new SqliteConnection(_conn);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Clients WHERE ClientCode LIKE $p";
            cmd.Parameters.AddWithValue("$p", prefix + "%");
            return prefix + (System.Convert.ToInt32(cmd.ExecuteScalar()) + 1).ToString("D3");
        }
    }
}



