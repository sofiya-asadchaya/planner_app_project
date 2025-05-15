using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Planner_app;

public partial class PlannerPage : Form
{
    private bool isDragging = false;
    private Panel draggedPanel = null;
    private Point dragOffset;
    private Panel originalParent;
    private Point originalLocation;
    private List<Panel> dayPanels = new List<Panel>();
    private Panel panelSource;
    private Panel panelTarget;
    private string stateFilePath = "Memory.json"; // Path for saving JSON state
    private Color originalColor;
    private DateTime lastSavedDate = DateTime.Today; // Initialize with today's date


    private IAppStateStorage appStateStorage;

    public PlannerPage()
    {
        InitializeComponent();
        this.Size = new Size(1200, 900);

        // Two versions of memory realisation are available chosen
        // appStateStorage = new JsonAppStateStorage("Memory.json");
        appStateStorage = new SQLiteAppStateStorage("PlannerApp.sqlite");

        InitializeUI();
        AdjustLayout();
        this.Resize += (s, e) => AdjustLayout();

        // Load state when the app starts
        LoadAppState();

        // Save state when the app closes
        this.FormClosing += (s, e) => SaveAppState();
    }

    private void SaveAppState()
    {
        AppState state = new AppState
        {
            LastSavedDate = DateTime.Today
        };

        CollectBlocks(panelSource.Controls, state);
        foreach (Panel dayPanel in dayPanels)
        {
            CollectBlocks(dayPanel.Controls, state);
        }

        appStateStorage.SaveAppState(state);
    }

    private void LoadAppState()
    {
        AppState state = appStateStorage.LoadAppState();

        if (state != null)
        {
            lastSavedDate = state.LastSavedDate;

            foreach (TaskBlockState taskState in state.TaskBlocks)
            {
                Panel taskBlock = CreateBlockPanel(taskState, "TaskBlock");
                AddBlockToPanel(taskBlock, taskState.ParentPanel, taskState.ParentDate);
            }

            foreach (TaskBlockState copyState in state.CopyBlocks)
            {
                Panel copyBlock = CreateBlockPanel(copyState, "CopyBlock");
                AddBlockToPanel(copyBlock, copyState.ParentPanel, copyState.ParentDate);
            }
        }
    }


    private void CollectBlocks(Control.ControlCollection controls, AppState state)
    {
        foreach (Control control in controls)
        {
            if (control is Panel blockPanel)
            {
                string blockTag = blockPanel.Tag?.ToString();
                if (blockTag == "TaskBlock" || blockTag == "CopyBlock")
                {
                    // Retrieve the date from the parent panel's Tag property 
                    DateTime? parentDate = null;
                    if (blockPanel.Parent?.Tag is DateTime dateTag)
                    {
                        parentDate = dateTag;
                    }

                    TaskBlockState blockState = new TaskBlockState
                    {
                        Text = blockPanel.Controls.OfType<Label>().FirstOrDefault()?.Text,
                        Color = ColorTranslator.ToHtml(blockPanel.BackColor),
                        ParentPanel = blockPanel.Parent?.Name,
                        ParentDate = parentDate, // Store the associated date
                        LocationX = blockPanel.Location.X,
                        LocationY = blockPanel.Location.Y,
                        Width = blockPanel.Width,
                        Height = blockPanel.Height,
                        Visible = blockPanel.Visible
                    };

                    // Add to appropriate list 
                    if (blockTag == "TaskBlock")
                    {
                        state.TaskBlocks.Add(blockState);
                    }
                    else if (blockTag == "CopyBlock")
                    {
                        state.CopyBlocks.Add(blockState);
                    }
                }
            }
        }
    }


    private Panel CreateBlockPanel(TaskBlockState blockState, string blockType)
    {
        Panel blockPanel = new Panel
        {
            BackColor = ColorTranslator.FromHtml(blockState.Color),
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(blockState.LocationX, blockState.LocationY),
            Size = new Size(blockState.Width, blockState.Height),
            Visible = blockState.Visible,
            Tag = blockType
        };

        Label label = new Label
        {
            Text = blockState.Text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Font = new Font("Arial", 10, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        blockPanel.Controls.Add(label);

        // Attach the MouseDown event for right-click functionality
        blockPanel.MouseDown += TaskBlock_MouseDown;  

        // Forward label events to the parent block
        label.MouseDown += (s, args) => TaskBlock_MouseDown(blockPanel, args);
        label.MouseMove += (s, args) => TaskBlock_MouseMove(blockPanel, args);
        label.MouseUp += (s, args) => TaskBlock_MouseUp(blockPanel, args);

        return blockPanel;
    }



    private void AddBlockToPanel(Panel blockPanel, string parentPanelName, DateTime? parentDate = null)
    {
        // If the parentPanelName is "panelSource", add the block to panelSource
        if (parentPanelName == panelSource.Name)
        {
            panelSource.Controls.Add(blockPanel);
        }
        else if (parentDate.HasValue)
        {
            // If the ParentDate is not null, find the corresponding day panel
            foreach (Panel dayPanel in dayPanels)
            {
                if (dayPanel.Tag is DateTime panelDate && panelDate.Date == parentDate.Value.Date)
                {
                    dayPanel.Controls.Add(blockPanel);
                    break;
                }
            }
        }
        else
        {
            // Fallback: Find the panel by name (if no date is provided)
            foreach (Panel dayPanel in dayPanels)
            {
                if (dayPanel.Name == parentPanelName)
                {
                    dayPanel.Controls.Add(blockPanel);
                    break;
                }
            }
        }
    }


    private void InitializeUI()
    {
        // Create source panel 
        panelSource = new Panel
        {
            Name = "panelSource",
            BackColor = ColorTranslator.FromHtml("#424242"),
            BorderStyle = BorderStyle.FixedSingle
        };

        // Create destination panel 
        panelTarget = new Panel
        {
            Name = "panelTarget",
            Size = new Size(this.ClientSize.Width, (int)(this.ClientSize.Height * 0.7)),
            Location = new Point(0, 0),
            BackColor = ColorTranslator.FromHtml("#424242"),
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true, // Enable scrolling in the target area
            HorizontalScroll = { Enabled = true } // Enable horizontal scroll specifically
        };

        // Add both panels to the form
        this.Controls.Add(panelSource);
        this.Controls.Add(panelTarget);

        // Attach a MouseDown event handler to the source panel for creating task blocks
        panelSource.MouseDown += PanelSource_MouseDown;

        // Initialize day panels once
        InitializeDayPanels();


        AddScaleButtonToSourcePanel();

    }

    private void PanelSource_MouseDown(object sender, MouseEventArgs e)
    {
        // Check if left mouse button is clicked
        if (e.Button == MouseButtons.Left)
        {
            // Open the block settings form to configure the block
            using (var settingsForm = new BlockSettingsForm())
            {
                if (settingsForm.ShowDialog() == DialogResult.OK)
                {
                    // Create a new draggable block
                    Panel taskBlock = new Panel
                    {
                        BackColor = settingsForm.BlockColor,
                        BorderStyle = BorderStyle.FixedSingle,
                        Location = e.Location, // Set the location to the mouse click position
                        Tag = "CopyBlock" // Add a tag to identify it as a task block
                    };

                    // Add a label to display the task text
                    Label label = new Label
                    {
                        Text = settingsForm.BlockText,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        ForeColor = Color.White,
                        Font = new Font("Arial", 10, FontStyle.Bold),
                        BackColor = Color.Transparent
                    };

                    // Forward label events to the parent block
                    label.MouseDown += (s, args) => TaskBlock_MouseDown(taskBlock, args);
                    label.MouseMove += (s, args) => TaskBlock_MouseMove(taskBlock, args);
                    label.MouseUp += (s, args) => TaskBlock_MouseUp(taskBlock, args);

                    // Attach drag event handlers to the block
                    taskBlock.MouseDown += TaskBlock_MouseDown;
                    taskBlock.MouseMove += TaskBlock_MouseMove;
                    taskBlock.MouseUp += TaskBlock_MouseUp;

                    // Add controls to the block
                    taskBlock.Controls.Add(label);

                    // Add the block to the source panel
                    panelSource.Controls.Add(taskBlock);

                    ResizeAndRepositionTaskBlocks();
                }
            }
        }
    }

    private DateTime lastCheckedDate = DateTime.Today; // Tracks the last checked date

    private void InitializeDayPanels()
    {
        
        DateTime today = DateTime.Today;

        
        DateTime startDate = today.AddDays(-2);

        // Clear existing panels from the target panel and list
        panelTarget.Controls.Clear();
        dayPanels.Clear();

        // Create the day panels directly inside the target panel
        for (int i = 0; i < 9; i++)
        {
            DateTime currentDay = startDate.AddDays(i); 

            Panel dayPanel = new Panel
            {
                BackColor = ColorTranslator.FromHtml("#B0B0B0"),
                BorderStyle = BorderStyle.FixedSingle,
                Name = $"dayPanel{i}",
                Tag = currentDay // Tag the panel with the corresponding date
            };

            // Add the day panel to the list and target panel
            dayPanels.Add(dayPanel);
            panelTarget.Controls.Add(dayPanel);

            // Create and add the label for the day name 
            Label dayNameLabel = new Label
            {
                Text = currentDay.ToString("ddd"), // Day abbreviation (e.g., Mon)
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top, 
                Font = new Font("Arial", 10, FontStyle.Bold),
                Height = 25 
            };
            dayPanel.Controls.Add(dayNameLabel);

            
            Label lblDate = new Label
            {
                Text = currentDay.ToString("MMM dd"), 
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Bottom, 
                Font = new Font("Arial", 9, FontStyle.Regular),
                Height = 25 
            };
            dayPanel.Controls.Add(lblDate);

            // Highlight the current day
            if (currentDay.Date == today.Date)
            {
                dayNameLabel.BackColor = Color.Yellow; // Highlight current day (e.g., yellow background)
            }
        }

        
        StartDayChangeTimer();
    }

    private void StartDayChangeTimer()
    {
        
        System.Windows.Forms.Timer dayChangeTimer = new System.Windows.Forms.Timer
        {
            Interval = 60000 // Check every 60 seconds
        };

        // Event handler for the timer's tick event
        dayChangeTimer.Tick += DayChangeTimer_Tick;
        dayChangeTimer.Start();
    }

    // Event handler that is triggered when the timer ticks
    private void DayChangeTimer_Tick(object sender, EventArgs e)
    {
        DateTime currentDate = DateTime.Today;

        
        if (lastSavedDate.Date != currentDate.Date)
        {
            lastSavedDate = currentDate;

            
            ShiftTasksToMatchDates();

            
            SaveAppState();
        }
    }


    private void ShiftTasksToMatchDates()
    {
        DateTime today = DateTime.Today;

        for (int i = 0; i < dayPanels.Count; i++)
        {
            Panel dayPanel = dayPanels[i];
            DateTime panelDate = (DateTime)dayPanel.Tag;

            // Update the date for the panel
            panelDate = panelDate.AddDays(1);
            dayPanel.Tag = panelDate;

            
            Label dateLabel = dayPanel.Controls.OfType<Label>().FirstOrDefault(lbl => lbl.Dock == DockStyle.Top);
            if (dateLabel != null)
            {
                dateLabel.Text = panelDate.ToString("ddd, MMM dd");
                dateLabel.BackColor = panelDate.Date == today ? Color.Yellow : Color.Transparent;
            }

            
            if (panelDate < today.AddDays(-2) || panelDate > today.AddDays(6))
            {
                dayPanel.Controls.Clear();
            }
        }
    }

    private void UpdateDayPanelLabels(DateTime startDate)
    {
        for (int i = 0; i < dayPanels.Count; i++)
        {
            DateTime currentDate = startDate.AddDays(i);
            Panel dayPanel = dayPanels[i];

            // Update day name label
            var dayNameLabel = dayPanel.Controls.OfType<Label>()
                .FirstOrDefault(lbl => lbl.Dock == DockStyle.Top);

            if (dayNameLabel != null)
            {
                dayNameLabel.Text = currentDate.DayOfWeek.ToString();
            }

            // Update date label
            var dateLabel = dayPanel.Controls.OfType<Label>()
                .FirstOrDefault(lbl => lbl.Dock == DockStyle.Bottom);

            if (dateLabel != null)
            {
                dateLabel.Text = currentDate.ToString("MMM dd");
            }
        }
    }






    private void AdjustLayout()
    {
        // Calculate dimensions based on form size
        int targetHeight = (int)(this.ClientSize.Height * 0.7); // 70% of height
        int sourceHeight = this.ClientSize.Height - targetHeight; // Remaining height (30%)

        // Update target panel 
        panelTarget.Location = new Point(0, 0); // Top of the form
        panelTarget.Size = new Size(this.ClientSize.Width, targetHeight); // Full width, 70% height

        // Update source panel 
        panelSource.Location = new Point(0, targetHeight); // Bottom of the form
        panelSource.Size = new Size(this.ClientSize.Width, sourceHeight); // Full width, 30% height

        // Adjust sizes and positions of day panels
        ResizeAndRepositionDayPanels();

        // Resize and reposition task blocks
        ResizeAndRepositionTaskBlocks();

        ResizeAndRepositionTaskBlocksInDayPanels();
    }

    private void ResizeAndRepositionDayPanels()
    {
        if (dayPanels.Count == 0) return;

        int availableHeight = panelTarget.Height;
        int availableWidth = panelTarget.Width;

        int dayPanelHeight = (int)(availableHeight * 0.9); // 90% of the height
        int dayPanelWidth = (int)(availableWidth * 0.2); // 20% of the available width for each day panel
        int xOffset = 10; // Spacing between day panels

        for (int i = 0; i < dayPanels.Count; i++)
        {
            Panel dayPanel = dayPanels[i];
            dayPanel.Size = new Size(dayPanelWidth, dayPanelHeight);
            dayPanel.Location = new Point(i * (dayPanelWidth + xOffset), 10); // Adjust location
        }
    }


    private void TaskBlock_MouseDown(object sender, MouseEventArgs e)
    {
        if (sender is Panel taskBlock)
        {
            // Right-click logic for color toggle or deletion
            if (e.Button == MouseButtons.Right)
            {
                // If the task block is in the source panel, delete it
                if (taskBlock.Parent == panelSource)
                {
                    panelSource.Controls.Remove(taskBlock); // Remove from the source panel
                }
                else
                {
                    // If it's in a day panel, toggle its color to gray or back to original
                    if (taskBlock.BackColor == Color.LightGray)
                    {
                        taskBlock.BackColor = originalColor; // Revert to original color
                    }
                    else
                    {
                        originalColor = taskBlock.BackColor; // Store the current color
                        taskBlock.BackColor = Color.LightGray; // Set to light gray
                    }
                }
            }
            else if (e.Button == MouseButtons.Left) // Left-click for dragging functionality
            {
                isDragging = true;
                draggedPanel = taskBlock;
                dragOffset = e.Location; // Record the offset of the mouse click within the block

                // Record the original location and parent
                originalLocation = taskBlock.Location;
                originalParent = taskBlock.Parent as Panel;

                // Bring the block to the front and add it directly to the form 
                this.Controls.Add(draggedPanel);
                draggedPanel.BringToFront();
            }
        }
    }


    private void TaskBlock_MouseMove(object sender, MouseEventArgs e)
    {
        if (isDragging && draggedPanel != null)
        {
            // Update block position relative to the form
            Point mousePosition = this.PointToClient(MousePosition);
            draggedPanel.Location = new Point(
                mousePosition.X - dragOffset.X,
                mousePosition.Y - dragOffset.Y
            );
        }
    }

    private void TaskBlock_MouseUp(object sender, MouseEventArgs e)
    {
        if (isDragging && draggedPanel != null)
        {
            // Flag to check if the block is dropped in any of the day panels
            bool isDroppedOnDayPanel = false;

            // Iterate through each day panel in the target area
            foreach (Panel dayPanel in dayPanels)
            {
                // Check if the block is dropped inside this specific day panel
                if (dayPanel.Bounds.Contains(PointToClient(MousePosition)))
                {
                    isDroppedOnDayPanel = true;

                    // Move the original block into the day panel
                    Point newLocation = dayPanel.PointToClient(MousePosition);
                    draggedPanel.Parent = dayPanel;
                    draggedPanel.Location = new Point(
                        newLocation.X - dragOffset.X,
                        newLocation.Y - dragOffset.Y
                    );
                    Tag = "TaskBlock";

                    // If the block is coming from the source panel, create a copy at the original location
                    if (originalParent == panelSource)
                    {
                        Panel copyBlock = new Panel
                        {
                            Size = draggedPanel.Size,
                            BackColor = draggedPanel.BackColor,
                            BorderStyle = draggedPanel.BorderStyle,
                            Location = originalLocation,
                            Tag = "CopyBlock"
                        };

                        // Find the label in the original block and create a copy
                        Label originalLabel = draggedPanel.Controls.OfType<Label>().FirstOrDefault();
                        if (originalLabel != null)
                        {
                            Label copyLabel = new Label
                            {
                                Text = originalLabel.Text,
                                Dock = originalLabel.Dock,
                                TextAlign = originalLabel.TextAlign,
                                ForeColor = originalLabel.ForeColor,
                                Font = originalLabel.Font,
                                BackColor = originalLabel.BackColor
                            };


                            // Forward label events to the parent block
                            copyLabel.MouseDown += (s, args) => TaskBlock_MouseDown(copyBlock, args);
                            copyLabel.MouseMove += (s, args) => TaskBlock_MouseMove(copyBlock, args);
                            copyLabel.MouseUp += (s, args) => TaskBlock_MouseUp(copyBlock, args);

                            copyBlock.Controls.Add(copyLabel);
                        }

                        // Attach drag event handlers to the copy
                        copyBlock.MouseDown += TaskBlock_MouseDown;
                        copyBlock.MouseMove += TaskBlock_MouseMove;
                        copyBlock.MouseUp += TaskBlock_MouseUp;

                        // Add the copy to the source panel
                        panelSource.Controls.Add(copyBlock);
                    }

                    break; 
                }
            }

            if (!isDroppedOnDayPanel)
            {
                // If the block is dropped inside the source panel
                if (panelSource.Bounds.Contains(PointToClient(MousePosition)))
                {
                    // Remove the block if it was dragged from a day panel
                    if (originalParent != panelSource)
                    {
                        draggedPanel.Dispose(); // Delete the dragged block
                    }
                    else
                    {
                        // Move the original block back into the source panel
                        Point newLocation = panelSource.PointToClient(MousePosition);
                        draggedPanel.Parent = panelSource;
                        draggedPanel.Location = new Point(
                            newLocation.X - dragOffset.X,
                            newLocation.Y - dragOffset.Y
                        );
                    }
                }
                else
                {
                    // If the block is not dropped inside any valid panel, return the block to its original location
                    draggedPanel.Parent = originalParent;
                    draggedPanel.Location = originalLocation;
                }
            }

            // Reset dragging state
            isDragging = false;
            draggedPanel = null;
        }
    }

    private void ResizeAndRepositionTaskBlocks()
    {
        // Check if there are any blocks in the source panel
        if (panelSource.Controls.Count == 0) return;

        // Get the available width and height in the source panel
        int availableHeight = panelSource.Height;
        int availableWidth = panelSource.Width;

        // Define new size parameters for the blocks
        int blockHeight = (int)(availableHeight * 0.3);
        int blockWidth = (int)(availableWidth * 0.15);

        // Define spacing between blocks
        int horizontalSpacing = 10;
        int verticalSpacing = 10;

        // Calculate the number of columns that can fit in the panel
        int columns = Math.Max(1, availableWidth / (blockWidth + horizontalSpacing));

        int blockIndex = 0; // Track the index for all blocks

        // Loop through all controls in the source panel
        foreach (Control control in panelSource.Controls)
        {
            // Ensure the control is a valid block type (TaskBlock or CopyBlock)
            if (control is Panel block &&
                (block.Tag?.ToString() == "TaskBlock" || block.Tag?.ToString() == "CopyBlock"))
            {
                // Calculate row and column based on the block index
                int row = blockIndex / columns;
                int col = blockIndex % columns;

                // Set new size for the block
                block.Size = new Size(blockWidth, blockHeight);

                // Calculate new position for the block
                int x = col * (blockWidth + horizontalSpacing);
                int y = row * (blockHeight + verticalSpacing);

                block.Location = new Point(x, y);

                blockIndex++; 
            }
        }
    }




    private void ResizeAndRepositionTaskBlocksInDayPanels()
    {
        if (dayPanels.Count == 0) return;

        // Loop through each day panel
        foreach (Panel dayPanel in dayPanels)
        {
            // Get the available width and height for each day panel
            int availableHeight = dayPanel.ClientSize.Height;
            int availableWidth = dayPanel.ClientSize.Width;

            // Check for a label or other header component at the top of the day panel
            int labelHeight = 0;
            foreach (Control control in dayPanel.Controls)
            {
                if (control is Label)
                {
                    labelHeight = Math.Max(labelHeight, control.Height);
                }
            }

            // Deduct the label height from the available height for task blocks
            int taskBlockAreaHeight = availableHeight - labelHeight;
            if (taskBlockAreaHeight <= 0) continue; // Skip if there's no space for task blocks

            // Set the size of the task blocks as a percentage of the remaining height
            int taskBlockHeight = (int)(taskBlockAreaHeight * 0.1); // Adjust percentage as needed
            int taskBlockWidth = availableWidth; // Full width of the day panel
            int verticalSpacing = 5; // Space between task blocks

            int currentY = labelHeight; // Start positioning below the label

            // Adjust the position and size of each task block inside the day panel
            foreach (Control control in dayPanel.Controls)
            {
                if (control is Panel taskBlock) // Only process task blocks
                {
                    // Set the size of the task block
                    taskBlock.Size = new Size(taskBlockWidth, taskBlockHeight);

                    // Set the location to stack vertically
                    taskBlock.Location = new Point(0, currentY);

                    // Update the current Y position for the next block
                    currentY += taskBlockHeight + verticalSpacing;
                }
            }
        }
    }

    private void AddScaleButtonToSourcePanel()
    {
        // Create the button
        Button scaleButton = new Button
        {
            Size = new Size(50, 30), // Set the button size
            Text = "Scale", // Set the button text
            Font = new Font("Arial", 10, FontStyle.Regular),
            BackColor = Color.LightGray,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right, // Ensure it stays at the bottom-right
            Location = new Point(panelSource.ClientSize.Width - 50, panelSource.ClientSize.Height - 30)
        };

        // Attach the click event to trigger the resizing logic
        scaleButton.Click += (sender, e) => ResizeAndRepositionTaskBlocks();
        scaleButton.Click += (sender, e) => ResizeAndRepositionTaskBlocksInDayPanels();

        // Add the button to the source panel
        panelSource.Controls.Add(scaleButton);
    }
}

public class AppState
{
    public List<TaskBlockState> TaskBlocks { get; set; } = new List<TaskBlockState>();
    public List<TaskBlockState> CopyBlocks { get; set; } = new List<TaskBlockState>();
    public DateTime LastSavedDate { get; set; } // Store the last saved date
}

public class TaskBlockState
{
    public string Text { get; set; }
    public string Color { get; set; }
    public string ParentPanel { get; set; }
    public int LocationX { get; set; }
    public int LocationY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Visible { get; set; }
    public DateTime? ParentDate { get; set; }

}

//




