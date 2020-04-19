using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using GoogleCalendarManager.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GoogleCalendarManager.Data
{
    public class CalendarManagerService
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IEnumerable<string> eventsSummaryForDeletion;

        public CalendarManagerService(IHttpContextAccessor httpContextAccessor, CalendarManagerOptions calendarManagerOptions)
        {
            this.httpContextAccessor = httpContextAccessor;
            eventsSummaryForDeletion = calendarManagerOptions.EventsSummaryForDeletion;
        }

        public async Task<IEnumerable<CalendarEvent>> GetTodayUpcomingEvents()
        {
            Events events = await RequestEvents(new RequestEvenstArgs
            {
                TimeMax = DateTime.Now.Date.AddHours(24).AddSeconds(-1),
                TimeMin = DateTime.Now
            });

            return ProcessCalendarEvents(events, _ => true);
        }

        public async Task<IEnumerable<CalendarEvent>> GetPastEvenets(int days)
        {
            var events = await RequestEvents(new RequestEvenstArgs
            {
                TimeMax = DateTime.Now,
                TimeMin = DateTime.Now.Date.AddDays(days * -1)
            });

            return ProcessCalendarEvents(events, eventItem => eventItem.End.DateTime < DateTime.Now);
        }

        public async Task<IEnumerable<CalendarEvent>> GetEventsForDeletion(int days)
        {
            var pastEvents = await GetPastEvenets(days);

            return pastEvents.Where(calendarEvent => eventsSummaryForDeletion.Contains(calendarEvent.Summary));
        }

        public async Task DeleteEvenets(IEnumerable<CalendarEvent> calendarEvents)
        {
            foreach (var calendarEvent in calendarEvents)
            {
                await DeleteEvenet(calendarEvent.Id);
            }
        }

        public async Task DeleteEvenet(string eventId)
        {
            CalendarService service = await GetCalendarService();

            await service.Events.Delete("primary", eventId).ExecuteAsync();
        }

        private IEnumerable<CalendarEvent> ProcessCalendarEvents(Events events, Func<Event, bool> condition)
        {
            if (events.Items != null && events.Items.Count > 0)
            {
                foreach (var eventItem in events.Items)
                {
                    if (eventItem.Start.DateTime == null) eventItem.Start.DateTime = DateTime.Parse(eventItem.Start.Date);
                    if (eventItem.End.DateTime == null) eventItem.End.DateTime = DateTime.Parse(eventItem.End.Date).AddSeconds(-1);

                    if (condition(eventItem))
                    {
                        yield return new CalendarEvent
                        {
                            Id = eventItem.Id,
                            Summary = eventItem.Summary,
                            Start = eventItem.Start.DateTime.ToString(),
                            End = eventItem.End.DateTime.ToString()
                        };
                    }
                }
            }
        }

        private async Task<Events> RequestEvents(RequestEvenstArgs requestEvenstArgs)
        {
            CalendarService service = await GetCalendarService();
            EventsResource.ListRequest request = service.Events.List("primary");
            request.TimeMax = requestEvenstArgs.TimeMax;
            request.TimeMin = requestEvenstArgs.TimeMin;
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            return await request.ExecuteAsync();
        }

        private async Task<CalendarService> GetCalendarService()
        {
            var token = await httpContextAccessor.HttpContext.GetTokenAsync("access_token");

            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = GoogleCredential.FromAccessToken(token),
                ApplicationName = "Google Calendar API .NET Quickstart",
            });

            return service;
        }
    }
}
