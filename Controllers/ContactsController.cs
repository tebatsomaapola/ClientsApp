using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using ClientsApp.Models;
using System.Collections.Generic;

namespace ClientsApp.Controllers
{
    public class ContactsController : Controller
    {
        private readonly string _connectionString = "Data Source=clients.db";

        // Get all Contacts
        public IActionResult Index()
        {
            var viewModel = new ContactsViewModel();

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                //  Get contact List
                var contactCmd = connection.CreateCommand();
                contactCmd.CommandText = @"
                    SELECT Id, Name, Surname, Email, 
                    (SELECT COUNT(DISTINCT ClientCode) FROM Contacts c2 
                     WHERE c2.Name = Contacts.Name AND c2.Surname = Contacts.Surname 
                     AND c2.ClientCode IS NOT NULL) AS LinkedCount
                    FROM Contacts 
                    GROUP BY Name, Surname 
                    ORDER BY Name ASC, Surname ASC";

                using (var reader = contactCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        viewModel.Contacts.Add(new ContactDisplayItem {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Surname = reader.GetString(2),
                            Email = reader.GetString(3),
                            LinkedClientsCount = reader.GetInt32(4)
                        });
                    }
                }

                // Fetch Linked Clients 
                var linkedCmd = connection.CreateCommand();
                linkedCmd.CommandText = @"
                    SELECT cl.Name, cl.ClientCode, co.Id 
                    FROM Clients cl 
                    INNER JOIN Contacts co ON cl.ClientCode = co.ClientCode
                    ORDER BY cl.Name ASC";

                using (var reader = linkedCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        viewModel.LinkedClients.Add(new LinkedClientDisplayItem {
                            ClientName = reader.GetString(0),
                            ClientCode = reader.GetString(1),
                            ContactId = reader.GetInt32(2)
                        });
                    }
                }
            }

            return View(viewModel);
        }

        // Creat Contact
        [HttpPost]
        public IActionResult Create(Contact newContact)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var cmd = connection.CreateCommand();
                cmd.CommandText = "INSERT INTO Contacts (Name, Surname, Email) VALUES ($name, $surname, $email)";
                cmd.Parameters.AddWithValue("$name", newContact.Name);
                cmd.Parameters.AddWithValue("$surname", newContact.Surname);
                cmd.Parameters.AddWithValue("$email", newContact.Email);
                cmd.ExecuteNonQuery();
            }
            return RedirectToAction("Index");
        }

        // GET Contact to Unlink
        public IActionResult Unlink(int id)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using var transaction = connection.BeginTransaction();

                try
                {
                    //  Find the Client Code linked to the contact ID
                    var findCmd = connection.CreateCommand();
                    findCmd.CommandText = "SELECT ClientCode FROM Contacts WHERE Id = $id";
                    findCmd.Parameters.AddWithValue("$id", id);
                    string code = findCmd.ExecuteScalar()?.ToString();

                    if (!string.IsNullOrEmpty(code))
                    {
                        //  Decrement the clients linked contacts count
                        var decCmd = connection.CreateCommand();
                        decCmd.CommandText = "UPDATE Clients SET LinkedContactsCount = MAX(0, LinkedContactsCount - 1) WHERE ClientCode = $code";
                        decCmd.Parameters.AddWithValue("$code", code);
                        decCmd.ExecuteNonQuery();

                        // C. Remove the link from the contact record
                        var unlinkCmd = connection.CreateCommand();
                        unlinkCmd.CommandText = "UPDATE Contacts SET ClientCode = NULL WHERE Id = $id";
                        unlinkCmd.Parameters.AddWithValue("$id", id);
                        unlinkCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                }
            }
            return RedirectToAction("Index");
        }
    }
}

