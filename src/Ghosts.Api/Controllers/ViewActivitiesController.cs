// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace ghosts.api.Controllers;

[Controller]
[Produces("application/json")]
[Route("view-activities")]
[ApiExplorerSettings(IgnoreApi = true)]
public class ViewActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
        
    public ViewActivitiesController(ApplicationDbContext context)
    {
        _context = context;
    }
    
    [HttpGet]
    public IActionResult Index()
    {
        var list = this._context.Npcs.ToList().OrderBy(o => o.Enclave).ThenBy(o=>o.Team);
        return View("Index", list);
    }
    
    [HttpGet("{id:guid}")]
    public IActionResult Detail(Guid id)
    {
        var o = this._context.Npcs.FirstOrDefault(x => x.Id == id);
        return View("Detail", o);
    }
}