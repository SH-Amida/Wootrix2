﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Wootrix.Data;
using WootrixV2.Data;
using WootrixV2.Models;

namespace WootrixV2.Controllers
{
    public class CompanySegmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHostingEnvironment _env;
        private readonly string _rootpath;
        private readonly UserManager<ApplicationUser> _userManager;
        private ApplicationUser _user;
        private int _companyID;

        public CompanySegmentsController(UserManager<ApplicationUser> userManager, IHostingEnvironment env, ApplicationDbContext context)
        {
            _context = context;
            _env = env;
            _rootpath = _env.WebRootPath;
            _userManager = userManager;

        }



        // GET: CompanySegments
        public async Task<IActionResult> UserSegmentSearch(string SearchString)
        {
            _companyID = HttpContext.Session.GetInt32("CompanyID") ?? 0;

            var ctx = _context.CompanySegment
                 .Where(m => m.CompanyID == _companyID && (m.Title.Contains(SearchString) || m.Tags.Contains(SearchString)));
            return View(await ctx.ToListAsync());
        }


        // GET: CompanySegments
        public async Task<IActionResult> Index()
        {
            //Get the company name out the session and use it as a filter for the groups returned

            // We also need to filter on department.
            // So if a segment is set to only be Editable by Company Admins of Department IT, 
            // then if the current Company Admin is in Department Marketing they wont see it.
            // Note that if the segment doesn't have a department set, everyone sees it


            _companyID = HttpContext.Session.GetInt32("CompanyID") ?? 0;

            // Get current user department
            _user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            WootrixV2.Models.User un = _context.User.Where(x => x.EmailAddress == _user.Email).FirstOrDefaultAsync().GetAwaiter().GetResult();

            var department = un.Categories; //bad naming for the old DB i know
            IQueryable<CompanySegment> ctx;


            // If the user doesn't have a department don't filter on it
            if (!string.IsNullOrWhiteSpace(department))
            {
                ctx = _context.CompanySegment
                  .Where(m => m.CompanyID == _companyID)
                  .Where(m => m.Department == department);
            }
            else
            {
                ctx = _context.CompanySegment
                .Where(m => m.CompanyID == _companyID);
            }

            return View(await ctx.ToListAsync());
        }

        // GET: SegmentArticles/Articlelist/id of segment
        public async Task<IActionResult> ArticleList(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            HttpContext.Session.SetString("SegmentListID", id);
            _companyID = HttpContext.Session.GetInt32("CompanyID") ?? 0;
            var segmentArticle = _context.SegmentArticle
                .Where(n => n.CompanyID == _companyID)
                .Where(m => m.Segments.Contains(id));

            //Also add the Segment to the Viewbag so we can get the Image
            CompanySegment cs = await _context.CompanySegment.FirstOrDefaultAsync(m => m.Title == id && m.CompanyID == _companyID);
            ViewBag.Segment = cs;      
            if (segmentArticle == null)
            {
                return NotFound();
            }

            return View(await segmentArticle.ToListAsync());
        }

        // GET: SegmentArticles/Articlelist/id of segment
        public async Task<IActionResult> UserArticleSearch(string searchString)
        {
            if (searchString == null)
            {
                return NotFound();
            }

            var segmentTitle = HttpContext.Session.GetString("SegmentListID");
            _companyID = HttpContext.Session.GetInt32("CompanyID") ?? 0;

            var segmentArticle = _context.SegmentArticle
                .Where(n => n.CompanyID == _companyID)
                .Where(m => m.Segments.Contains(segmentTitle) && (m.Title.Contains(searchString) || m.Tags.Contains(searchString)));

            //Also add the Segment to the Viewbag so we can get the Image
            CompanySegment cs = await _context.CompanySegment.FirstOrDefaultAsync(m => m.Title == segmentTitle && m.CompanyID == _companyID);
            ViewBag.Segment = cs;
            if (segmentArticle == null)
            {
                return NotFound();
            }

            return View(await segmentArticle.ToListAsync());
        }


        // GET: CompanySegments/Details/5
        public async Task<IActionResult> Details(int id)
        {

            _companyID = HttpContext.Session.GetInt32("CompanyID") ?? 0;
            CompanySegment cs = await _context.CompanySegment.FirstOrDefaultAsync(m => m.ID == id && m.CompanyID == _companyID);

            var segmentArticle = _context.SegmentArticle
                .Where(n => n.CompanyID == cs.CompanyID)
                .Where(m => m.Segments.Contains(cs.Title));

            //Also add the Segment to the Viewbag so we can get the Image
            ViewBag.Segment = cs;

            if (segmentArticle == null)
            {
                return NotFound();
            }

            return View(await segmentArticle.ToListAsync());
        }

        // GET: CompanySegments/Create
        public IActionResult Create()
        {
            _user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            CompanySegmentViewModel s = new CompanySegmentViewModel();
            s.Order = 1;
            s.PublishDate = DateTime.Now.Date;
            s.FinishDate = DateTime.Now.AddYears(10).Date;
            s.StandardColor = HttpContext.Session.GetString("CompanyHeaderBackgroundColor");
            s.ThemeColor = HttpContext.Session.GetString("CompanyHighlightColor");
            s.ClientName = _user.name;
            //s.ClientLogoImage = _user.photoUrl;
            var cp = _user.companyID;

            DatabaseAccessLayer dla = new DatabaseAccessLayer(_context);
            s.Departments = dla.GetDepartments(cp);
            return View(s);
        }

        // POST: CompanySegments/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CompanySegmentViewModel cps)
        {
            _user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            var companyID = HttpContext.Session.GetInt32("CompanyID") ?? 0;
            //Initialise a new companysegment
            var mySegment = new CompanySegment();
            if (ModelState.IsValid)
            {
                //ID,Order,Title,CoverImage,CoverImageMobileFriendly,PublishDate,FinishDate,ClientName,ClientLogoImage,ThemeColor,StandardColor,Draft,Department,Tags
                mySegment.CompanyID = companyID;
                mySegment.Order = cps.Order ?? 1;
                mySegment.Title = cps.Title;
                mySegment.PublishDate = cps.PublishDate;
                mySegment.FinishDate = cps.FinishDate;
                mySegment.ClientName = cps.ClientName;
                mySegment.ThemeColor = cps.ThemeColor;
                mySegment.StandardColor = cps.StandardColor;
                mySegment.Draft = DateTime.Now > cps.PublishDate ? false : true;
                mySegment.Department = cps.Department;
                mySegment.Tags = cps.Tags;
                mySegment.ClientName = cps.ClientName ?? _user.name;

                IFormFile coverImage = cps.CoverImage;
                if (coverImage != null)
                {
                    var filePath = Path.Combine(_rootpath, "images/Uploads", _user.companyName + "_" + coverImage.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await coverImage.CopyToAsync(stream);
                    }
                    //The file has been saved to disk - now save the file name to the DB
                    mySegment.CoverImage = coverImage.FileName;
                }

                IFormFile coverImageMB = cps.CoverImageMobileFriendly;
                if (coverImageMB != null)
                {
                    var filePath = Path.Combine(_rootpath, "images/Uploads", _user.companyName + "_" + coverImageMB.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await coverImageMB.CopyToAsync(stream);
                    }
                    //The file has been saved to disk - now save the file name to the DB
                    mySegment.CoverImageMobileFriendly = coverImageMB.FileName;
                }

                IFormFile cli = cps.ClientLogoImage;
                if (cli != null)
                {
                    var filePath = Path.Combine(_rootpath, "images/Uploads", _user.companyName + "_" + cli.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await cli.CopyToAsync(stream);
                    }
                    //The file has been saved to disk - now save the file name to the DB
                    mySegment.CoverImageMobileFriendly = cli.FileName;
                }

                _context.Add(mySegment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            DatabaseAccessLayer dla = new DatabaseAccessLayer(_context);
            cps.Departments = dla.GetDepartments(companyID);
            return View(cps);
        }

        // GET: CompanySegments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var companySegment = await _context.CompanySegment.FindAsync(id);
            if (companySegment == null)
            {
                return NotFound();
            }

            _user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            CompanySegmentViewModel s = new CompanySegmentViewModel();

            s.Order = companySegment.Order;
            s.Title = companySegment.Title;
            s.PublishDate = companySegment.PublishDate;
            s.FinishDate = companySegment.FinishDate;
            s.StandardColor = companySegment.StandardColor;
            s.ThemeColor = companySegment.ThemeColor;
            s.ClientName = (companySegment.ClientName == null ? _user.name : companySegment.ClientName);
            // s.ClientLogoImage = FormFileHelper.PhysicalToIFormFile(new FileInfo(companySegment.ClientLogoImage));
            s.Department = companySegment.Department;
            s.Tags = companySegment.Tags;

            DatabaseAccessLayer dla = new DatabaseAccessLayer(_context);
            s.Departments = dla.GetDepartments(_user.companyID);
            return View(s);
        }

        // POST: CompanySegments/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CompanySegmentViewModel cps)
        {
            if (id != cps.ID)
            {
                return NotFound();
            }

            _user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            //Initialise a new companysegment
            CompanySegment mySegment = await _context.CompanySegment.FindAsync(id);

            if (ModelState.IsValid)
            {
                //ID,Order,Title,CoverImage,CoverImageMobileFriendly,PublishDate,FinishDate,ClientName,ClientLogoImage,ThemeColor,StandardColor,Draft,Department,Tags
                mySegment.CompanyID = _user.companyID;
                mySegment.Order = cps.Order ?? 1;
                mySegment.Title = cps.Title;
                mySegment.PublishDate = cps.PublishDate;
                mySegment.FinishDate = cps.FinishDate;
                mySegment.ClientName = cps.ClientName;
                mySegment.ThemeColor = cps.ThemeColor;
                mySegment.StandardColor = cps.StandardColor;
                mySegment.Draft = DateTime.Now > cps.PublishDate ? false : true;
                mySegment.Department = cps.Department;
                mySegment.Tags = cps.Tags;

                IFormFile coverImage = cps.CoverImage;
                if (coverImage != null)
                {
                    var filePath = Path.Combine(_rootpath, "images/Uploads", _user.companyName + "_" + coverImage.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await coverImage.CopyToAsync(stream);
                    }
                    //The file has been saved to disk - now save the file name to the DB
                    mySegment.CoverImage = coverImage.FileName;
                }

                IFormFile coverImageMB = cps.CoverImageMobileFriendly;
                if (coverImageMB != null)
                {
                    var filePath = Path.Combine(_rootpath, "images/Uploads", _user.companyName + "_" + coverImageMB.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await coverImageMB.CopyToAsync(stream);
                    }
                    //The file has been saved to disk - now save the file name to the DB
                    mySegment.CoverImageMobileFriendly = coverImageMB.FileName;
                }

                IFormFile cli = cps.ClientLogoImage;
                if (cli != null)
                {
                    var filePath = Path.Combine(_rootpath, "images/Uploads", _user.companyName + "_" + cli.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await cli.CopyToAsync(stream);
                    }
                    //The file has been saved to disk - now save the file name to the DB
                    mySegment.CoverImageMobileFriendly = cli.FileName;
                }

                try
                {
                    _context.Update(mySegment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CompanySegmentExists(cps.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            DatabaseAccessLayer dla = new DatabaseAccessLayer(_context);
            cps.Departments = dla.GetDepartments(_user.companyID);
            return View(cps);
        }

        // GET: CompanySegments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var companySegment = await _context.CompanySegment
                .FirstOrDefaultAsync(m => m.ID == id);
            if (companySegment == null)
            {
                return NotFound();
            }

            return View(companySegment);
        }

        // POST: CompanySegments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var companySegment = await _context.CompanySegment.FindAsync(id);
            _context.CompanySegment.Remove(companySegment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CompanySegmentExists(int id)
        {
            return _context.CompanySegment.Any(e => e.ID == id);
        }
    }
}
