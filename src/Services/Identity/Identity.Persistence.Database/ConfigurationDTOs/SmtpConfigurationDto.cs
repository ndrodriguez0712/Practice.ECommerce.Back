﻿namespace Identity.Service.Queries.DTOs
{
    public class SmtpConfigurationDto
    {
        public string Server { get; set; }
        public int Port { get; set; }
        public string From { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
