﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Accelerider.Windows.Infrastructure;

namespace Accelerider.Windows
{
    internal class AcceleriderUser : IAcceleriderUser
    {
        public string Token { get; set; }

        public string Id { get; set; }

        public string Email { get; set; }

        public bool EmailVisibility { get; set; }

        public string Username { get; set; }

        public Uri AvatarUrl { get; set; }

        public string MultiOnline { get; set; }

        public IList<string> Roles { get; set; }

        public IList<string> Apps { get; set; }

        public virtual Task<bool> RefreshAsync()
        {
            return Task.FromResult(true);
        }
    }
}
