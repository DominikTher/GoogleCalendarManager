using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GoogleCalendarManager.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authentication.Google;
using Google.Apis.Calendar.v3;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System;
using System.Linq;

namespace GoogleCalendarManager
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddRazorPages()
                .AddMvcOptions(setup => setup.Filters.Add(new AuthorizeFilter()));
            services.AddServerSideBlazor();
            services.AddScoped<CalendarManagerService>();
            services.Configure<CalendarManagerOptions>(options =>
            {
                options.EventsSummaryForDeletion = Configuration.GetSection("EventsSummaryForDeletion").Get<IEnumerable<string>>();
            });
            services.AddSingleton(service => service.GetRequiredService<IOptions<CalendarManagerOptions>>().Value);
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddGoogle(
                    GoogleDefaults.AuthenticationScheme,
                    GoogleDefaults.DisplayName,
                    options =>
                    {
                        options.ClientId = Configuration["ClientId"];
                        options.ClientSecret = Configuration["ClientSecret"];
                        options.Scope.Add(CalendarService.Scope.CalendarEvents);
                        options.SaveTokens = true;

                        options.Events = new OAuthEvents
                        {
                            OnCreatingTicket = ctx =>
                            {
                                ctx.Identity.AddClaim(new Claim("access_token", ctx.AccessToken));

                                return Task.CompletedTask;
                            }
                        };
                    });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
